﻿// <copyright file="ConstFinder.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    internal static class ConstFinder
    {
        public static bool HasParsedSolution { get; private set; } = false;

        public static List<(string key, string qualification, string value, string source)> KnownConsts { get; } = new List<(string key, string qualification, string value, string source)>();

        public static string[] SearchValues
        {
            get
            {
                return KnownConsts.Select(c => c.key).ToArray();
            }
        }

        public static async Task TryParseSolutionAsync(IComponentModel componentModel = null)
        {
            if (componentModel == null)
            {
                componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            }

            var workspace = (Workspace)componentModel.GetService<VisualStudioWorkspace>();

            if (workspace == null)
            {
                return;
            }

            var projectGraph = workspace.CurrentSolution?.GetProjectDependencyGraph();

            if (projectGraph == null)
            {
                return;
            }

            foreach (ProjectId projectId in projectGraph.GetTopologicallySortedProjects())
            {
                Compilation projectCompilation = await workspace.CurrentSolution?.GetProject(projectId).GetCompilationAsync();

                if (projectCompilation != null)
                {
                    foreach (var compiledTree in projectCompilation.SyntaxTrees)
                    {
                        GetConstsFromSyntaxRoot(await compiledTree.GetRootAsync(), compiledTree.FilePath);
                    }
                }
            }

            HasParsedSolution = true;
        }

        public static async Task ReloadConstsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));

            if (ConstFinder.HasParsedSolution)
            {
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                var activeDocument = dte?.ActiveDocument;
                if (activeDocument != null)
                {
                    var workspace = (Workspace)componentModel.GetService<VisualStudioWorkspace>();
                    var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(activeDocument.FullName).FirstOrDefault();
                    if (documentId != null)
                    {
                        var document = workspace.CurrentSolution.GetDocument(documentId);

                        await TrackConstsInDocumentAsync(document);
                    }
                }
            }
            else
            {
                await ConstFinder.TryParseSolutionAsync(componentModel);
            }
        }

        public static async Task<bool> TrackConstsInDocumentAsync(Document document)
        {
            System.Diagnostics.Debug.WriteLine(document.FilePath);

            if (document.FilePath.Contains(".g.")
                || document.FilePath.Contains(".Designer."))
            {
                return false;
            }

            var result = true;

            if (document.TryGetSyntaxTree(out SyntaxTree _))
            {
                var root = await document.GetSyntaxRootAsync();

                GetConstsFromSyntaxRoot(root, document.FilePath);
            }

            return result;
        }

        public static void GetConstsFromSyntaxRoot(SyntaxNode root, string filePath)
        {
            // Avoid parsing generated code.
            // Reduces overhead (as there may be lots)
            // Avoids assets included with Android projects.
            if (filePath.ToLowerInvariant().EndsWith(".designer.cs")
             || filePath.ToLowerInvariant().EndsWith(".g.cs")
             || filePath.ToLowerInvariant().EndsWith(".g.i.cs"))
            {
                return;
            }

            var toRemove = new List<(string, string, string, string)>();

            foreach (var item in KnownConsts)
            {
                if (item.source == filePath)
                {
                    toRemove.Add(item);
                }
            }

            foreach (var item in toRemove)
            {
                KnownConsts.Remove(item);
            }

            foreach (var vdec in root.DescendantNodes().OfType<VariableDeclarationSyntax>())
            {
                if (vdec != null)
                {
                    if (vdec.Parent is MemberDeclarationSyntax dec)
                    {
                        if (IsConst(dec))
                        {
                            if (dec is FieldDeclarationSyntax fds)
                            {
                                var qualification = GetQualification(fds);

                                foreach (var variable in fds.Declaration.Variables)
                                {
                                    KnownConsts.Add(
                                        (variable.Identifier.Text,
                                         qualification,
                                         variable.Initializer.Value.ToString().Replace("\\\"", "\""),
                                         filePath));
                                }
                            }
                        }
                    }
                    else
                    {
                        if (vdec.Parent is LocalDeclarationStatementSyntax ldec)
                        {
                            if (IsConst(ldec))
                            {
                                System.Diagnostics.Debug.WriteLine(ldec);
                            }
                        }
                    }
                }
            }
        }

        public static string GetQualification(MemberDeclarationSyntax dec)
        {
            var result = string.Empty;
            var parent = dec.Parent;

            while (parent != null)
            {
                if (parent is ClassDeclarationSyntax cds)
                {
                    result = $"{cds.Identifier.ValueText}.{result}";
                    parent = cds.Parent;
                }
                else if (parent is NamespaceDeclarationSyntax nds)
                {
                    result = $"{nds.Name}.{result}";
                    parent = nds.Parent;
                }
                else
                {
                    parent = parent.Parent;
                }
            }

            return result.TrimEnd('.');
        }

        public static bool IsConst(SyntaxNode node)
        {
            return node.ChildTokens().Any(t => t.IsKind(SyntaxKind.ConstKeyword));
        }

        internal static void Reset()
        {
            KnownConsts.Clear();
            HasParsedSolution = false;
        }

        internal static string GetDisplayText(string constName, string qualifier, string fileName)
        {
            var constsInThisFile =
                KnownConsts.Where(c => c.source == fileName
                                    && c.key == constName
                                    && c.qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(constsInThisFile.value))
            {
                return constsInThisFile.value;
            }

            var (_, _, value, _) =
                KnownConsts.Where(c => c.key == constName
                                    && c.qualification.EndsWith(qualifier)).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return string.Empty;
        }
    }
}

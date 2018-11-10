﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [ProvideAutoLoad(UIContextGuids.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(VSPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class VSPackage : AsyncPackage
    {
        /// <summary>
        /// VSPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "8c14dc72-9022-42ff-a85c-1cfe548a8956";

        /// <summary>
        /// Initializes a new instance of the <see cref="VSPackage"/> class.
        /// </summary>
        public VSPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            //Use nested methods to avoid prompt (and need) for MainThead check/switch
            IEnumerable<ProjectItem> RecurseProjectItems(ProjectItems projItems)
            {
                if (projItems != null)
                {
                    foreach (ProjectItem item in projItems)
                    {
                        foreach (var subItem in RecurseProjectItem(item))
                        {
                            yield return subItem;
                        }
                    }
                }
            }

            IEnumerable<ProjectItem> RecurseProjectItem(ProjectItem item)
            {
                yield return item;
                foreach (var subItem in RecurseProjectItems(item.ProjectItems))
                {
                    yield return subItem;
                }
            }

            IEnumerable<ProjectItem> GetProjectFiles(Project proj)
            {
                    foreach (ProjectItem item in RecurseProjectItems(proj.ProjectItems))
                    {
                        yield return item;
                    }
            }

            // TODO: handle res files being removed or added to a project - currently will be ignored
            // Get all resource files from the solution
            // Do this now, rather than in adornment manager for performance and to avoid thread issues
            if (await this.GetServiceAsync(typeof(DTE)) is DTE dte)
            {
                foreach (var project in (Array)dte.ActiveSolutionProjects)
                {
                    foreach (var solFile in GetProjectFiles((Project)project))
                    {
                        var filePath = solFile.FileNames[0];
                        var fileExt = System.IO.Path.GetExtension(filePath);

                        // Only interested in resource files
                        if (fileExt.Equals(".resx") || fileExt.Equals(".resw"))
                        {
                            // Only want neutral language ones, not locale specific versions
                            if (!System.IO.Path.GetFileNameWithoutExtension(filePath).Contains("."))
                            {
                                ResourceAdornmentManager.ResourceFiles.Add(filePath);
                            }
                        }
                    }
                }

                (await this.GetServiceAsync(typeof(DTE)) as DTE).StatusBar.Text = $"Initialized StringResourceVisualizer with {ResourceAdornmentManager.ResourceFiles.Count} resource files.";
            }
        }
    }
}

// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace StringResAdorners
{
    /// <summary>
    /// Important class. Handles creation of adornments on appropriate lines.
    /// </summary>
    internal class ResourceAdornmentManager : IDisposable
    {
        private IAdornmentLayer layer;
        private IWpfTextView view;

        /// <summary>
        /// Initializes static members of the <see cref="ResourceAdornmentManager"/> class.
        /// </summary>
        static ResourceAdornmentManager()
        {
            ResourceFiles = new List<string>();
        }

        public ResourceAdornmentManager(IWpfTextView view)
        {
            this.view = view;
            this.layer = view.GetAdornmentLayer("StringResourceCommentLayer");

            this.Resources = new Dictionary<int, TextBlock>();

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static AsyncPackage Package { get; set; }
        public static List<string> ResourceFiles { get; set; }

        // Dictionary to map line number to UI displaying text
        public Dictionary<int, TextBlock> Resources { get; set; }

        /// <summary>
        /// This is called by the TextView when closing. Events are unsubscribed here.
        /// </summary>
        /// <remarks>
        /// It's actually called twice - once by the IPropertyOwner instance, and again by the ITagger instance.
        /// </remarks>
        public void Dispose() => this.UnsubscribeFromViewerEvents();

        /// <summary>
        /// On layout change add the adornment to any reformatted lines.
        /// </summary>
        private async void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
        {
            this.Resources.Clear();

            // TODO: clear cached resource file here
            foreach (ITextViewLine line in this.view.TextViewLines)
            {
                int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;
                try
                {
                    await this.CreateVisualsAsync(line, lineNumber);
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        private async System.Threading.Tasks.Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        {
            try
            {
                string lineText = line.Extent.GetText();

                // TODO: make it work with any Resource file
                const string SearchText = "StringRes.";

                // TODO: support use of multiple resources in the same line
                int matchIndex = lineText.IndexOf(SearchText);
                if (matchIndex >= 0)
                {
                    if (!this.Resources.ContainsKey(lineNumber))
                    {
                        // Get coordinates of text
                        int start = line.Extent.Start.Position + matchIndex;
                        int end = line.Start + (line.Extent.Length - 1);
                        var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(start, end));

                        var endPos = lineText.IndexOfAny(new[] { ' ', '.', '"', '(', ')' }, matchIndex + SearchText.Length);

                        string foundText;

                        if (endPos > matchIndex)
                        {
                            foundText = lineText.Substring(matchIndex, endPos - matchIndex);
                        }
                        else
                        {
                            foundText = lineText.Substring(matchIndex);
                        }

                        string displayText = "???" + foundText;

                        if (Package != null)
                        {
                            // TODO: Address Main Thread Issue
                            var dte = await Package.GetServiceAsync(typeof(DTE)) as DTE;

                            if (dte != null)
                            {
                                var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);

                                var project = ((Array)dte.ActiveSolutionProjects).GetValue(0) as Project;

                                // TODO: Support resx & resw
                                var resxPath = this.SolutionFiles(project).FirstOrDefault(f => f.FileNames[0].EndsWith("StringRes.resx", StringComparison.InvariantCultureIgnoreCase));

                                if (resxPath != null)
                                {
                                    // TODO: cache xml file
                                    var xdoc = new XmlDocument();
                                    xdoc.Load(resxPath.FileNames[0]);

                                    foreach (XmlElement element in xdoc.GetElementsByTagName("data"))
                                    {
                                        if (element.GetAttribute("name") == resourceName)
                                        {
                                            var valueElement = element.GetElementsByTagName("value").Item(0);
                                            displayText = valueElement.InnerText;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // TODO: freeze the brush
                        // TODO: color should be from Visualstuidio resources
                        // TODO: adjust height
                        TextBlock tb = new TextBlock();
                        tb.Foreground = new SolidColorBrush(Colors.Gray);
                        tb.Text = $"\"{displayText}\"";
                        tb.Height = 20;

                        // TODO: check still need this
                        var finalRect = default(System.Windows.Rect);
                        tb.Arrange(finalRect);

                        // TODO: review need for this (might be an async issue)
                        if (!this.Resources.ContainsKey(lineNumber))
                        {
                            this.Resources.Add(lineNumber, tb);
                        }

                        var lineGeometry = this.view.TextViewLines.GetMarkerGeometry(span);

                        Canvas.SetLeft(tb, lineGeometry.Bounds.Left);
                        Canvas.SetTop(tb, line.TextTop - tb.Height);

                        this.layer.RemoveAdornment(tb);
                        this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, tag: null, adornment: tb, removedCallback: null);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private IEnumerable<ProjectItem> Recurse(ProjectItems i)
        {
            if (i != null)
            {
                foreach (ProjectItem j in i)
                {
                    foreach (var k in this.Recurse(j))
                    {
                        yield return k;
                    }
                }
            }
        }

        private IEnumerable<ProjectItem> Recurse(ProjectItem i)
        {
            yield return i;
            foreach (var j in this.Recurse(i.ProjectItems))
            {
                yield return j;
            }
        }

        private IEnumerable<ProjectItem> SolutionFiles(EnvDTE.Project project)
        {
            foreach (ProjectItem item in this.Recurse(project.ProjectItems))
            {
                yield return item;
            }
        }

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}

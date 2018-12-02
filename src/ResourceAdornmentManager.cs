// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    /// <summary>
    /// Important class. Handles creation of adornments on appropriate lines.
    /// </summary>
    internal class ResourceAdornmentManager : IDisposable
    {
        private readonly IAdornmentLayer layer;
        private readonly IWpfTextView view;

        public ResourceAdornmentManager(IWpfTextView view)
        {
            this.view = view;
            this.layer = view.GetAdornmentLayer("StringResourceCommentLayer");

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static List<string> ResourceFiles { get; set; } = new List<string>();

        public static List<string> SearchValues { get; set; } = new List<string>();

        public static List<(string path, XmlDocument xDoc)> XmlDocs { get; private set; } = new List<(string path, XmlDocument xDoc)>();

        public static bool ResourcesLoaded { get; private set; }

        // Initialize to the same default as VS
        public static uint TextSize { get; set; } = 10;

        // Initialize to a reasonable value for display on light or dark themes/background.
        public static Color TextForegroundColor { get; set; } = Colors.Gray;

        public static FileSystemWatcher ResxWatcher { get; private set; } = new FileSystemWatcher();

        // Keep a record of displayed text blocks so we can remove them as soon as changed or no longer appropriate
        // Also use this to identify lines to pad so the textblocks can be seen
        public Dictionary<int, (TextBlock textBlock, string resName)> DisplayedTextBlocks { get; set; } = new Dictionary<int, (TextBlock textBlock, string resName)>();

        public static void LoadResources(List<string> resxFilesOfInterest, string slnDirectory)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await TaskScheduler.Default;

                ResourcesLoaded = false;

                ResourceFiles.Clear();
                SearchValues.Clear();
                XmlDocs.Clear();

                foreach (var resourceFile in resxFilesOfInterest)
                {
                    await Task.Yield();

                    try
                    {
                        var doc = new XmlDocument();
                        doc.Load(resourceFile);

                        XmlDocs.Add((resourceFile, doc));
                        ResourceFiles.Add(resourceFile);

                        var searchTerm = $"{Path.GetFileNameWithoutExtension(resourceFile)}.";

                        if (!SearchValues.Contains(searchTerm))
                        {
                            SearchValues.Add(searchTerm);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }

                if (resxFilesOfInterest.Any())
                {
                    // Need to track changed and renamed events as VS doesn't do a direct overwrite but makes a temp file of the new version and then renames both files.
                    // Changed event will also pick up changes made by extensions or programs other than VS.
                    ResxWatcher.Filter = "*.resx";
                    ResxWatcher.Path = slnDirectory;
                    ResxWatcher.IncludeSubdirectories = true;
                    ResxWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                    ResxWatcher.Changed -= ResxWatcher_Changed;
                    ResxWatcher.Changed += ResxWatcher_Changed;
                    ResxWatcher.Renamed -= ResxWatcher_Renamed;
                    ResxWatcher.Renamed += ResxWatcher_Renamed;
                    ResxWatcher.EnableRaisingEvents = true;
                }
                else
                {
                    ResxWatcher.EnableRaisingEvents = false;
                    ResxWatcher.Changed -= ResxWatcher_Changed;
                    ResxWatcher.Renamed -= ResxWatcher_Renamed;
                }

                ResourcesLoaded = true;
            });
        }

        private static async void ResxWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            // Don't want to know about files being named from .resx to something else
            if (e.FullPath.EndsWith(".resx"))
            {
                await ReloadResourceFileAsync(e.FullPath);
            }
        }

        private static async void ResxWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            await ReloadResourceFileAsync(e.FullPath);
        }

        private static async Task ReloadResourceFileAsync(string filePath)
        {
            const int maxAttemptCount = 5;
            const int baseWaitPeriod = 250;

            ResourcesLoaded = false;

            for (var i = 0; i < XmlDocs.Count; i++)
            {
                var xmlDoc = XmlDocs[i];

                if (xmlDoc.path == filePath)
                {
                    // File may still be locked after being moved/renamed/updated
                    // Allow for retry after delay with back-off.
                    for (var attempted = 0; attempted < maxAttemptCount; attempted++)
                    {
                        try
                        {
                            if (attempted > 0)
                            {
                                await Task.Delay(attempted * baseWaitPeriod);
                            }

                            var doc = new XmlDocument();
                            doc.Load(filePath);

                            XmlDocs[i] = (xmlDoc.path, doc);
                        }
                        catch (Exception ex)
                        {
                            // If never load the changed file just stick with the previously loaded version.
                            // Hopefully get updated version after next change.
                            Debug.WriteLine(ex);
                        }
                    }

                    break;
                }
            }

            ResourcesLoaded = true;
        }

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
        private void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (ResourcesLoaded)
            {
                foreach (ITextViewLine line in this.view.TextViewLines)
                {
                    int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;

                    try
                    {
                        this.CreateVisuals(line, lineNumber);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }
        }

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        private void CreateVisuals(ITextViewLine line, int lineNumber)
        {
            try
            {
                string lineText = line.Extent.GetText();

                // Remove any textblocks displayed on this line so it won't conflict with anything we add below.
                // Handles no textblocks to show or the text to display having changed.
                if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                {
                    this.layer.RemoveAdornment(this.DisplayedTextBlocks[lineNumber].textBlock);
                    this.DisplayedTextBlocks.Remove(lineNumber);
                }

                // TODO: need to handle multiple search texts being found on a line. Issue #4
                int matchIndex = lineText.IndexOfAny(SearchValues.ToArray());

                if (matchIndex == -1)
                {
                    // Remove the TextBlock if no longer needed
                    if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                    {
                        this.layer.RemoveAdornment(this.DisplayedTextBlocks[lineNumber].textBlock);
                        this.DisplayedTextBlocks.Remove(lineNumber);
                    }
                }
                else
                {
                    var endPos = lineText.IndexOfAny(new[] { ' ', '.', ',', '"', '(', ')', '}' }, lineText.IndexOf('.', matchIndex) + 1);

                    var foundText = endPos > matchIndex ? lineText.Substring(matchIndex, endPos - matchIndex) : lineText.Substring(matchIndex);

                    if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                    {
                        // Remove existing TextBlock as no longer matches placeholder
                        if (foundText != this.DisplayedTextBlocks[lineNumber].resName)
                        {
                            this.layer.RemoveAdornment(this.DisplayedTextBlocks[lineNumber].textBlock);
                            this.DisplayedTextBlocks.Remove(lineNumber);
                        }
                    }

                    if (!this.DisplayedTextBlocks.ContainsKey(lineNumber))
                    {
                        string displayText = null;

                        if (ResourceFiles.Any())
                        {
                            var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);

                            foreach (var (path, xDoc) in XmlDocs)
                            {
                                // As may be multiple resource files, only check the ones which have the correct name.
                                // If multiple projects in the solutions with same resource name (file & name), but different res value, the wrong value *may* be displayed
                                if (foundText.StartsWith($"{Path.GetFileNameWithoutExtension(path)}."))
                                {
                                    foreach (XmlElement element in xDoc.GetElementsByTagName("data"))
                                    {
                                        if (element.GetAttribute("name") == resourceName)
                                        {
                                            var valueElement = element.GetElementsByTagName("value").Item(0);
                                            displayText = valueElement?.InnerText;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(displayText) && TextSize > 0)
                        {
                            var brush = new SolidColorBrush(TextForegroundColor);
                            brush.Freeze();

                            const double textBlockSizeToFontScaleFactor = 1.4;

                            var tb = new TextBlock
                            {
                                Foreground = brush,
                                Text = $"\"{displayText}\"",
                                FontSize = TextSize,
                                Height = TextSize * textBlockSizeToFontScaleFactor
                            };

                            this.DisplayedTextBlocks.Add(lineNumber, (tb, foundText));

                            // Get coordinates of text
                            int start = line.Extent.Start.Position + matchIndex;
                            int end = line.Start + (line.Extent.Length - 1);
                            var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(start, end));
                            var lineGeometry = this.view.TextViewLines.GetMarkerGeometry(span);

                            Canvas.SetLeft(tb, lineGeometry.Bounds.Left);
                            Canvas.SetTop(tb, line.TextTop - tb.Height);

                            this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, tag: null, adornment: tb, removedCallback: null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}

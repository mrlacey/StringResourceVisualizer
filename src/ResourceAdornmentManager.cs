// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace StringResourceVisualizer
{
    /// <summary>
    /// Important class. Handles creation of adornments on appropriate lines.
    /// </summary>
    internal class ResourceAdornmentManager : IDisposable
    {
        private readonly IAdornmentLayer layer;
        private readonly IWpfTextView view;
        private List<(string path, XmlDocument xDoc)> xmlDocs = null;

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

            this.DisplayedTextBlocks = new Dictionary<int, TextBlock>();

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static List<string> ResourceFiles { get; set; }

        public static int TextSize { get; set; }

        public static Color TextForegroundColor { get; set; }

        // Keep a record of displayed text blocks so we can remove them as soon as changed or no longer appropriate
        // Also use this to identify lines to pad so the textblocks can be seen
        public Dictionary<int, TextBlock> DisplayedTextBlocks { get; set; }

        public List<(string path, XmlDocument xDoc)> XmlDocs
        {
            get
            {
                if (xmlDocs == null)
                {
                    try
                    {
                        xmlDocs = new List<(string, XmlDocument)>();

                        foreach (var resourceFile in ResourceFiles)
                        {
                            var xdoc = new XmlDocument();
                            xdoc.Load(resourceFile);

                            xmlDocs.Add((resourceFile, xdoc));
                        }

                    }
                    catch (Exception exc)
                    {
                        Debug.WriteLine(exc);
                    }
                }

                return xmlDocs;
            }
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
            if (ResourceFiles.Any())
            {
                // Determine text to search for (based on file names)
                var searchTexts = new string[ResourceFiles.Count];

                for (int i = 0; i < ResourceFiles.Count; i++)
                {
                    searchTexts[i] = $"{Path.GetFileNameWithoutExtension(ResourceFiles[i])}.";
                }

                this.ResetXmlDocs();

                foreach (ITextViewLine line in this.view.TextViewLines)
                {
                    int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;
                    try
                    {
                        this.CreateVisuals(line, lineNumber, searchTexts);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }
        }

        private void ResetXmlDocs()
        {
            // Set this to null so the resource files are re-read when next needed.
            // This will pick up any changes that have happened.
            this.xmlDocs = null;
        }

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        //private async System.Threading.Tasks.Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        private void CreateVisuals(ITextViewLine line, int lineNumber, string[] searchTexts)
        {
            try
            {
                string lineText = line.Extent.GetText();

                // Remove any textblock displayed on this line so it won't conflict with anything we add below.
                // Handles no textblock to show or the text to display having changed.
                if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                {
                    this.layer.RemoveAdornment(this.DisplayedTextBlocks[lineNumber]);
                    this.DisplayedTextBlocks.Remove(lineNumber);
                }

                // TODO: need to handle multiple search texts being found on a line. Issue #4
                int matchIndex = lineText.IndexOfAny(searchTexts);

                if (matchIndex >= 0)
                {
                    if (!this.DisplayedTextBlocks.ContainsKey(lineNumber))
                    {
                        var endPos = lineText.IndexOfAny(new[] { ' ', '.', ',' , '"', '(', ')', '}' }, lineText.IndexOf('.', matchIndex) + 1);

                        string foundText;

                        if (endPos > matchIndex)
                        {
                            foundText = lineText.Substring(matchIndex, endPos - matchIndex);
                        }
                        else
                        {
                            foundText = lineText.Substring(matchIndex);
                        }

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
                                            displayText = valueElement.InnerText;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(displayText))
                        {
                            var brush = new SolidColorBrush(TextForegroundColor);
                            brush.Freeze();

                            const double TextBlockSizeToFontScaleFactor = 1.4;

                            TextBlock tb = new TextBlock
                            {
                                Foreground = brush,
                                Text = $"\"{displayText}\"",
                                FontSize = TextSize,
                                Height = (TextSize * TextBlockSizeToFontScaleFactor)
                            };

                            this.DisplayedTextBlocks.Add(lineNumber, tb);

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

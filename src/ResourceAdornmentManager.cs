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
        private List<XmlDocument> xmlDocs = null;

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

            this.ResourcesToAdorn = new Dictionary<int, TextBlock>();

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static List<string> ResourceFiles { get; set; }

        public static int TextSize { get; set; }

        public static Color TextForegroundColor { get; set; }

        // Dictionary to map line number to UI displaying text
        public Dictionary<int, TextBlock> ResourcesToAdorn { get; set; }

        public List<XmlDocument> XmlDocs
        {
            get
            {
                if (xmlDocs == null)
                {
                    try
                    {
                        xmlDocs = new List<XmlDocument>();

                        foreach (var resourceFile in ResourceFiles)
                        {
                            var xdoc = new XmlDocument();
                            xdoc.Load(resourceFile);

                            xmlDocs.Add(xdoc);
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
                this.ResourcesToAdorn.Clear();

                // Determine text to search for (based on file names)
                var searchTexts = new string[ResourceFiles.Count];

                for (int i = 0; i < ResourceFiles.Count; i++)
                {
                    searchTexts[i] = $"{Path.GetFileNameWithoutExtension(ResourceFiles[i])}.";
                }

                ////// TODO: Will need to clear this here when have the ability to handle resource files added to project once opened Issue #2
                ////XmlDocs.Clear();

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

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        //private async System.Threading.Tasks.Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        private void CreateVisuals(ITextViewLine line, int lineNumber, string[] searchTexts)
        {
            try
            {
                string lineText = line.Extent.GetText();

                int matchIndex = lineText.IndexOfAny(searchTexts);

                if (matchIndex >= 0)
                {
                    if (!this.ResourcesToAdorn.ContainsKey(lineNumber))
                    {
                        // Get coordinates of text
                        int start = line.Extent.Start.Position + matchIndex;
                        int end = line.Start + (line.Extent.Length - 1);
                        var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(start, end));

                        var endPos = lineText.IndexOfAny(new[] { ' ', '.', '"', '(', ')' }, lineText.IndexOf('.', matchIndex) + 1);

                        string foundText;

                        if (endPos > matchIndex)
                        {
                            foundText = lineText.Substring(matchIndex, endPos - matchIndex);
                        }
                        else
                        {
                            foundText = lineText.Substring(matchIndex);
                        }

                        // TODO: Don't display anything if can't find actual text
                        string displayText = "???" + foundText;

                        if (ResourceFiles.Any())
                        {
                            var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);

                            foreach (var xdoc in XmlDocs)
                            {
                                // TODO: don't just go through every XML doc, check based on name if likely to contain expected value
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

                        var brush = new SolidColorBrush(TextForegroundColor);
                        brush.Freeze();

                        TextBlock tb = new TextBlock
                        {
                            Foreground = brush,
                            Text = $"\"{displayText}\"",
                            FontSize = TextSize,
                            Height = (TextSize * 1.4)
                        };

                        // TODO: check still need this
                        var finalRect = default(System.Windows.Rect);
                        tb.Arrange(finalRect);

                        // TODO: review need for this (might be an async issue)
                        if (!this.ResourcesToAdorn.ContainsKey(lineNumber))
                        {
                            this.ResourcesToAdorn.Add(lineNumber, tb);
                        }

                        var lineGeometry = this.view.TextViewLines.GetMarkerGeometry(span);

                        Canvas.SetLeft(tb, lineGeometry.Bounds.Left);
                        Canvas.SetTop(tb, line.TextTop - tb.Height);

                        // Check need this
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

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}

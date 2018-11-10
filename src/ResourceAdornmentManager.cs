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
        private void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
        {
            this.Resources.Clear();

            // TODO: clear cached resource file here
            foreach (ITextViewLine line in this.view.TextViewLines)
            {
                int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;
                try
                {
                    //await this.CreateVisualsAsync(line, lineNumber);
                    this.CreateVisuals(line, lineNumber);
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
        //private async System.Threading.Tasks.Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        private void CreateVisuals(ITextViewLine line, int lineNumber)
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

                        if (ResourceFiles.Any())
                        {
                                var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);

                            var resxPath = ResourceFiles.First();

                                    // TODO: cache xml file
                                    var xdoc = new XmlDocument();
                                    xdoc.Load(resxPath);

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

                        // TODO: color should be from Visualstuidio resources
                        var brush  = new SolidColorBrush(Colors.Gray);
                        brush.Freeze();

                        // TODO: adjust height
                        TextBlock tb = new TextBlock
                        {
                            Foreground = brush,
                            Text = $"\"{displayText}\"",
                            Height = 20
                        };

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

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}

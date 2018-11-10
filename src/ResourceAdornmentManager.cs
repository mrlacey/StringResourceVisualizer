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
using Microsoft.VisualStudio.PlatformUI;
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

            this.ResourcesToAdorn = new Dictionary<int, TextBlock>();

            this.view.LayoutChanged += this.LayoutChangedHandler;
        }

        public static List<string> ResourceFiles { get; set; }

        public static int TextSize { get; set; }

        public static Color TextForegroundColor { get; set; }

        // Dictionary to map line number to UI displaying text
        public Dictionary<int, TextBlock> ResourcesToAdorn { get; set; }

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

                // TODO: pass this in
                string[] searchTexts = new string[ResourceFiles.Count];

                for (int i = 0; i < ResourceFiles.Count; i++)
                {
                    searchTexts[i] = $"{Path.GetFileNameWithoutExtension(ResourceFiles[i])}.";
                }

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

                        // TODO: review whether should display anything if can't find actual text
                        string displayText = "???" + foundText;

                        if (ResourceFiles.Any())
                        {
                            var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);

                            // TODO: handle multiple res files
                            var resxPath = ResourceFiles.First();

                            // TODO: cache xml files
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

                        var brush  = new SolidColorBrush(TextForegroundColor);
                        brush.Freeze();

                        // TODO: adjust height
                        TextBlock tb = new TextBlock
                        {
                            Foreground = brush,
                            Text = $"\"{displayText}\"",
                            FontSize = TextSize,
                            Height = 20
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

    public static class StringExtensions
    {
        public static int IndexOfAny(this string source, params string[] values)
        {
            var valuePostions = new Dictionary<string, int>();

            foreach (var value in values)
            {
                valuePostions.Add(value, source.IndexOf(value));
            }

            if (valuePostions.Any(v => v.Value > -1))
            {
                var result = valuePostions.Select(v => v.Value).Where(v => v > -1).OrderByDescending(v => v).FirstOrDefault();

                return result;
            }

            return -1;
        }
    }
}

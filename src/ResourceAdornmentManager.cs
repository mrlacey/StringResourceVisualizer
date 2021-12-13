// <copyright file="ResourceAdornmentManager.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.TextManager.Interop;
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
        private readonly string fileName;
        private readonly List<(string alias, int lineNo, string resName)> aliases = new List<(string alias, int lineNo, string resName)>();
        private bool hasDoneInitialCreateVisualsPass = false;

        public ResourceAdornmentManager(IWpfTextView view)
        {
            this.view = view;
            this.layer = view.GetAdornmentLayer("StringResourceCommentLayer");

            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            this.fileName = this.GetFileName(view.TextBuffer);

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

        public static string PreferredCulture { get; private set; }

        public static bool SupportAspNetLocalizer { get; private set; }

        public static bool SupportNamespaceAliases { get; private set; }

        // Keep a record of displayed text blocks so we can remove them as soon as changed or no longer appropriate
        // Also use this to identify lines to pad so the textblocks can be seen
        public Dictionary<int, List<(TextBlock textBlock, string resName)>> DisplayedTextBlocks { get; set; } = new Dictionary<int, List<(TextBlock textBlock, string resName)>>();

        public static async Task LoadResourcesAsync(List<string> resxFilesOfInterest, string slnDirectory, string preferredCulture, OptionsGrid options)
        {
            await TaskScheduler.Default;

            ResourcesLoaded = false;

            ResourceFiles.Clear();
            SearchValues.Clear();
            XmlDocs.Clear();

            // Store this as will need it when looking up which text to use in the adornment.
            PreferredCulture = preferredCulture;
            SupportAspNetLocalizer = options.SupportAspNetLocalizer;
            SupportNamespaceAliases = options.SupportNamespaceAliases;

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

                    if (!string.IsNullOrWhiteSpace(preferredCulture)
                     && searchTerm.EndsWith($"{preferredCulture}.", StringComparison.InvariantCultureIgnoreCase))
                    {
                        searchTerm = searchTerm.Substring(0, searchTerm.Length - preferredCulture.Length - 1);
                    }

                    if (!SearchValues.Contains(searchTerm))
                    {
                        SearchValues.Add(searchTerm);
                    }
                }
                catch (Exception e)
                {
                    await OutputPane.Instance?.WriteAsync("Error loading resources");
                    await OutputPane.Instance?.WriteAsync(e.Message);
                    await OutputPane.Instance?.WriteAsync(e.Source);
                    await OutputPane.Instance?.WriteAsync(e.StackTrace);
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
        }

        public string GetFileName(ITextBuffer textBuffer)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var rc = textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDoc);

            if (rc == true)
            {
                return textDoc.FilePath;
            }
            else
            {
                rc = textBuffer.Properties.TryGetProperty(typeof(IVsTextBuffer), out IVsTextBuffer vsTextBuffer);

                if (rc)
                {
                    if (vsTextBuffer is IPersistFileFormat persistFileFormat)
                    {
                        persistFileFormat.GetCurFile(out string filePath, out _);
                        return filePath;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// This is called by the TextView when closing. Events are unsubscribed here.
        /// </summary>
        /// <remarks>
        /// It's actually called twice - once by the IPropertyOwner instance, and again by the ITagger instance.
        /// </remarks>
        public void Dispose() => this.UnsubscribeFromViewerEvents();

#pragma warning disable VSTHRD100 // Avoid async void methods
        private static async void ResxWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // Don't want to know about files being named from .resx to something else
                if (e.FullPath.EndsWith(".resx"))
                {
                    await ReloadResourceFileAsync(e.FullPath);
                }
            }
            catch (Exception exc)
            {
                await OutputPane.Instance?.WriteAsync("Unexpected error when resx file renamed.");
                await OutputPane.Instance?.WriteAsync(exc.Message);
                await OutputPane.Instance?.WriteAsync(exc.StackTrace);
            }
        }

        private static async void ResxWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                await ReloadResourceFileAsync(e.FullPath);
            }
            catch (Exception exc)
            {
                await OutputPane.Instance?.WriteAsync("Unexpected error when resx file changed.");
                await OutputPane.Instance?.WriteAsync(exc.Message);
                await OutputPane.Instance?.WriteAsync(exc.StackTrace);
            }
        }
#pragma warning restore VSTHRD100 // Avoid async void methods

        private static async Task ReloadResourceFileAsync(string filePath)
        {
            await OutputPane.Instance?.WriteAsync($"(Re)loading {filePath}");

            const int maxAttemptCount = 5;
            const int baseWaitPeriod = 250;

            ResourcesLoaded = false;

            for (var i = 0; i < XmlDocs.Count; i++)
            {
                var (path, _) = XmlDocs[i];

                if (path == filePath)
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

                            XmlDocs[i] = (path, doc);
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
        /// On layout change add the adornment to any reformatted lines.
        /// </summary>
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (ResourcesLoaded)
            {
                var collection = this.hasDoneInitialCreateVisualsPass ? (IEnumerable<ITextViewLine>)e.NewOrReformattedLines : this.view.TextViewLines;

                foreach (ITextViewLine line in collection)
                {
                    int lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;

                    try
                    {
                        await this.CreateVisualsAsync(line, lineNumber);
                    }
                    catch (InvalidOperationException ex)
                    {
                        await OutputPane.Instance?.WriteAsync("Error handling layout changed");
                        await OutputPane.Instance?.WriteAsync(ex.Message);
                        await OutputPane.Instance?.WriteAsync(ex.Source);
                        await OutputPane.Instance?.WriteAsync(ex.StackTrace);
                    }

                    this.hasDoneInitialCreateVisualsPass = true;
                }
            }
        }

        /// <summary>
        /// Scans text line for use of resource class, then adds new adornment.
        /// </summary>
        private async Task CreateVisualsAsync(ITextViewLine line, int lineNumber)
        {
            const string localizerIndicator = "localizer[";

            string GetDisplayTextFromDoc(XmlDocument xDoc, string key)
            {
                string result = null;

                foreach (XmlElement element in xDoc.GetElementsByTagName("data"))
                {
                    if (element.GetAttribute("name") == key)
                    {
                        var valueElement = element.GetElementsByTagName("value").Item(0);
                        result = valueElement?.InnerText;

                        if (result != null)
                        {
                            var returnIndex = result.IndexOfAny(new[] { '\r', '\n' });

                            if (returnIndex == 0)
                            {
                                result = result.TrimStart(' ', '\r', '\n');
                                returnIndex = result.IndexOfAny(new[] { '\r', '\n' });

                                if (returnIndex >= 0)
                                {
                                    // Truncate at first wrapping character and add "Return Character" to indicate truncation
                                    result = "⏎" + result.Substring(0, returnIndex) + "⏎";
                                }
                                else
                                {
                                    result = "⏎" + result;
                                }
                            }
                            else if (returnIndex > 0)
                            {
                                // Truncate at first wrapping character and add "Return Character" to indicate truncation
                                result = result.Substring(0, returnIndex) + "⏎";
                            }
                        }

                        break;
                    }
                }

                return result;
            }

            // TODO: Cache text retrieved from the resource file based on fileName and key. - Invalidate the cache when reload resource files. This will save querying the XMLDocument each time.
            try
            {
                if (!ResourceFiles.Any())
                {
                    // If there are no known resource files then there's no point doing anything that follows.
                    return;
                }

                string lineText = line.Extent.GetText();

                // The extent will include all of a collapsed section
                if (lineText.Contains(Environment.NewLine))
                {
                    // We only want the first "line" here as that's all that can be seen on screen
                    lineText = lineText.Substring(0, lineText.IndexOf(Environment.NewLine, StringComparison.InvariantCultureIgnoreCase));
                }

                string[] searchArray = SearchValues.ToArray();

                if (SupportNamespaceAliases)
                {
                    if (lineText.StartsWith("using ") & lineText.Contains(" = "))
                    {
                        // If a line with a known alias has changed forget everything we know about aliases and reload them all.
                        // Edge cases may temporarily be lost at this point but only if multiple aliases are specified in a file and there are many lones between them.
                        if (this.aliases.Any(a => a.lineNo == lineNumber))
                        {
                            this.aliases.Clear();
                        }

                        foreach (var searchTerm in SearchValues)
                        {
                            if (lineText.Trim().EndsWith($".{searchTerm.Replace(".", string.Empty)};"))
                            {
                                // 6 = "using ".Length
                                var alias = lineText.Substring(6, lineText.IndexOf(" = ") - 6).Trim();

                                // Add the dot here as it will save adding it for each line when look for usage.
                                this.aliases.Add(($"{alias}.", lineNumber, searchTerm));
                            }
                        }
                    }

                    searchArray = SearchValues.Concat(this.aliases.Select(a => a.alias).ToList()).ToArray();
                }

                // Remove any textblocks displayed on this line so it won't conflict with anything we add below.
                // Handles no textblocks to show or the text to display having changed.
                if (this.DisplayedTextBlocks.ContainsKey(lineNumber))
                {
                    foreach (var (textBlock, _) in this.DisplayedTextBlocks[lineNumber])
                    {
                        this.layer.RemoveAdornment(textBlock);
                    }

                    this.DisplayedTextBlocks.Remove(lineNumber);
                }

                var indexes = await lineText.GetAllIndexesAsync(searchArray);

                List<int> localizerIndexes;

                if (SupportAspNetLocalizer)
                {
                    localizerIndexes = await lineText.GetAllIndexesCaseInsensitiveAsync(localizerIndicator);

                    indexes.AddRange(localizerIndexes);
                }
                else
                {
                    localizerIndexes = new List<int>();
                }

                if (indexes.Any())
                {
                    var lastLeft = double.NaN;

                    // Reverse the list to can go through them right-to-left so know if there's anything that might overlap
                    indexes.Reverse();

                    foreach (var matchIndex in indexes)
                    {
                        int endPos = -1;
                        string foundText = string.Empty;
                        string displayText = null;

                        // If the localizer setting isn't enabled this definitely wont match.
                        if (localizerIndexes.Contains(matchIndex))
                        {
                            var lineSearchStart = matchIndex + localizerIndicator.Length;

                            var locClosingPos = lineText.IndexOf(']', lineSearchStart);

                            var locKey = lineText.Substring(lineSearchStart, locClosingPos - lineSearchStart);

                            if (locKey.StartsWith("\""))
                            {
                                var closingQuotePos = lineText.IndexOf('"', lineSearchStart + 1);

                                if (closingQuotePos > -1)
                                {
                                    foundText = lineText.Substring(lineSearchStart + 1, closingQuotePos - lineSearchStart - 1);
                                }
                            }
                            else
                            {
                                // key is a constant so need to look up the value
                                var lastDot = locKey.LastIndexOf('.');

                                var qualifier = string.Empty;
                                var constName = locKey;

                                if (lastDot >= 0)
                                {
                                    qualifier = locKey.Substring(0, lastDot);
                                    constName = locKey.Substring(lastDot + 1);
                                }

                                foundText = ConstFinder.GetDisplayText(constName, qualifier, this.fileName).Trim('"');
                            }

                            if (!string.IsNullOrEmpty(foundText))
                            {
                                foreach (var xDoc in this.GetLocalizerDocsOfInterest(this.fileName, XmlDocs, PreferredCulture))
                                {
                                    displayText = GetDisplayTextFromDoc(xDoc, foundText);

                                    if (!string.IsNullOrWhiteSpace(displayText))
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            endPos = lineText.IndexOfAny(new[] { ' ', '.', ',', '"', '(', ')', '{', '}', ';' }, lineText.IndexOf('.', matchIndex) + 1);

                            foundText = endPos > matchIndex
                                ? lineText.Substring(matchIndex, endPos - matchIndex)
                                : lineText.Substring(matchIndex);

                            var resourceName = foundText.Substring(foundText.IndexOf('.') + 1);
                            var fileBaseName = foundText.Substring(0, foundText.IndexOf('.'));

                            if (SupportNamespaceAliases)
                            {
                                // Look for alias use
                                var alias = this.aliases.FirstOrDefault(a => a.alias == $"{fileBaseName}.");

                                if (alias.resName != null)
                                {
                                    // Substitute for the value the alias represents.
                                    fileBaseName = alias.resName.Replace(".", string.Empty);
                                }
                            }

                            foreach (var (_, xDoc) in this.GetDocsOfInterest(fileBaseName, XmlDocs, PreferredCulture))
                            {
                                displayText = GetDisplayTextFromDoc(xDoc, resourceName);

                                if (!string.IsNullOrWhiteSpace(displayText))
                                {
                                    break;
                                }
                            }
                        }

                        if (!this.DisplayedTextBlocks.ContainsKey(lineNumber))
                        {
                            this.DisplayedTextBlocks.Add(lineNumber, new List<(TextBlock textBlock, string resName)>());
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
                                Height = TextSize * textBlockSizeToFontScaleFactor,
                            };

                            this.DisplayedTextBlocks[lineNumber].Add((tb, foundText));

                            // Get coordinates of text
                            int start = line.Extent.Start.Position + matchIndex;
                            int end = line.Start + (line.Extent.Length - 1);
                            var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(start, end));
                            var lineGeometry = this.view.TextViewLines.GetMarkerGeometry(span);

                            if (!double.IsNaN(lastLeft))
                            {
                                tb.MaxWidth = lastLeft - lineGeometry.Bounds.Left - 5; // Minus 5 for padding
                                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                            }

                            Canvas.SetLeft(tb, lineGeometry.Bounds.Left);
                            Canvas.SetTop(tb, line.TextTop - tb.Height);

                            lastLeft = lineGeometry.Bounds.Left;

                            this.layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, tag: null, adornment: tb, removedCallback: null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await OutputPane.Instance?.WriteAsync("Error creating visuals");
                await OutputPane.Instance?.WriteAsync(ex.Message);
                await OutputPane.Instance?.WriteAsync(ex.Source);
                await OutputPane.Instance?.WriteAsync(ex.StackTrace);
            }
        }

        private IEnumerable<(string path, XmlDocument xDoc)> GetDocsOfInterest(string resourceBaseName, List<(string path, XmlDocument xDoc)> xmlDocs, string preferredCulture)
        {
            // As may be multiple resource files, only check the ones which have the correct name.
            // If multiple projects in the solutions with same resource name (file & name), but different res value, the wrong value *may* be displayed
            // Get preferred cutlure files first
            if (!string.IsNullOrWhiteSpace(preferredCulture))
            {
                var cultureDocs = xmlDocs.Where(x => Path.GetFileNameWithoutExtension(x.path).Equals($"{resourceBaseName}.{preferredCulture}", StringComparison.InvariantCultureIgnoreCase));
                foreach (var item in cultureDocs)
                {
                    yield return item;
                }
            }

            // Then default resource files
            var defaultResources = xmlDocs.Where(x => Path.GetFileNameWithoutExtension(x.path).Equals(resourceBaseName));
            foreach (var item in defaultResources)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Get list of resource docs that are most likley to match based on naming & folder structure.
        /// </summary>
        private IEnumerable<XmlDocument> GetLocalizerDocsOfInterest(string filePathName, List<(string path, XmlDocument xDoc)> xmlDocs, string preferredCulture)
        {
            var filteredXmlDocs = new List<(string resPath, string codePath, XmlDocument xDoc)>();

            // Rationalize paths that may be based on folders or dotted file names by converting dots to directory separators and treat all as folders
            // strip matching starts and then do a reverse match on the rationalized paths
            // - match preferred culture then no culture at each level
            (string, string) StripMatchingStart(string resPath, string codePath)
            {
                var rp = resPath.Substring(0, resPath.LastIndexOf('.'));
                var cp = codePath.Substring(0, codePath.LastIndexOf('.'));

                var maxLen = Math.Min(rp.Length, cp.Length);

                var sameLength = 0;

                for (int i = 0; i < maxLen; i++)
                {
                    if (rp[i] != cp[i])
                    {
                        sameLength = i;
                        break;
                    }
                }

                return (rp.Substring(sameLength).Replace('.', '\\'), cp.Substring(sameLength).Replace('.', '\\'));
            }

            var uniqueRelativeCodePaths = new List<string>();

            foreach (var (xdocPath, fxDoc) in xmlDocs)
            {
                var (stripedResPath, strippedCodePath) = StripMatchingStart(xdocPath, filePathName);

                if (!uniqueRelativeCodePaths.Contains(strippedCodePath))
                {
                    uniqueRelativeCodePaths.Add(strippedCodePath);
                }

                filteredXmlDocs.Add((stripedResPath, strippedCodePath, fxDoc));
            }

            var rawName = Path.GetFileNameWithoutExtension(filePathName);

            var pathsOfDocsReturned = new List<string>();

            foreach (var rationalizedCodePath in uniqueRelativeCodePaths)
            {
                var rawParts = rationalizedCodePath.Split('\\');

                for (int i = rawParts.Count() - 1; i > 0; i--)
                {
                    var withCultureSuffix = Path.Combine(string.Join("\\", rawParts.Take(i)), rawName, preferredCulture);

                    var wcs = filteredXmlDocs.FirstOrDefault((r) => r.resPath.Equals(withCultureSuffix, StringComparison.InvariantCultureIgnoreCase)
                                                                 || r.resPath.Substring(r.resPath.IndexOf("\\") + 1).Equals(withCultureSuffix, StringComparison.InvariantCultureIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(wcs.resPath))
                    {
                        pathsOfDocsReturned.Add(wcs.resPath);
                        yield return wcs.xDoc;
                    }

                    var withCultureFolder = Path.Combine(string.Join("\\", rawParts.Take(i)), preferredCulture, rawName);

                    var wcf = filteredXmlDocs.FirstOrDefault((r) => r.resPath.Equals(withCultureFolder, StringComparison.InvariantCultureIgnoreCase)
                                                                 || r.resPath.Substring(r.resPath.IndexOf("\\") + 1).Equals(withCultureFolder, StringComparison.InvariantCultureIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(wcf.resPath))
                    {
                        pathsOfDocsReturned.Add(wcf.resPath);
                        yield return wcf.xDoc;
                    }

                    var withoutCulture = Path.Combine(string.Join("\\", rawParts.Take(i)), rawName);

                    var wc = filteredXmlDocs.FirstOrDefault((r) => r.resPath.Equals(withoutCulture, StringComparison.InvariantCultureIgnoreCase)
                                                                 || r.resPath.Substring(r.resPath.IndexOf("\\") + 1).Equals(withoutCulture, StringComparison.InvariantCultureIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(wc.resPath))
                    {
                        pathsOfDocsReturned.Add(wc.resPath);
                        yield return wc.xDoc;
                    }
                }
            }

            // In case we haven't found the relevant file above (acutal file naming doesn't match expectations),
            // return the others to avoid not finding it at all.
            foreach (var fxd in filteredXmlDocs)
            {
                if (!pathsOfDocsReturned.Contains(fxd.resPath))
                {
                    yield return fxd.xDoc;
                }
            }
        }

        private void UnsubscribeFromViewerEvents()
        {
            this.view.LayoutChanged -= this.LayoutChangedHandler;
        }
    }
}

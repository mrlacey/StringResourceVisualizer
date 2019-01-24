// <copyright file="VSPackage.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
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
    [InstalledProductRegistration("#110", "#112", "1.4")] // Info on this package for Help/About
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

        public FileSystemWatcher SlnWatcher { get; private set; } = new FileSystemWatcher();

        public FileSystemWatcher ProjWatcher { get; private set; } = new FileSystemWatcher();

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

            // Since this package might not be initialized until after a solution has finished loading,
            // we need to check if a solution has already been loaded and then handle it.
            bool isSolutionLoaded = await this.IsSolutionLoadedAsync(cancellationToken);

            if (isSolutionLoaded)
            {
                await this.HandleOpenSolutionAsync(cancellationToken);
            }

            // Listen for subsequent solution events
            SolutionEvents.OnAfterOpenSolution += this.HandleOpenSolution;

            await this.LoadSystemTextSettingsAsync(cancellationToken);
        }

        private async Task<bool> IsSolutionLoadedAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!(await this.GetServiceAsync(typeof(SVsSolution)) is IVsSolution solService))
            {
                throw new ArgumentNullException(nameof(solService));
            }

            ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object value));

            return value is bool isSolOpen && isSolOpen;
        }

        private void HandleOpenSolution(object sender, EventArgs e)
        {
            this.JoinableTaskFactory.RunAsync(() => this.HandleOpenSolutionAsync(this.DisposalToken)).Task.LogAndForget("StringResourceVisualizer");
        }

        private async Task HandleOpenSolutionAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await OutputPane.Instance.WriteAsync("If you have problems, or suggestions for improvement, report them at https://github.com/mrlacey/StringResourceVisualizer/issues/new ");
            await OutputPane.Instance.WriteAsync("If you like this extension please leave a review at https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.StringResourceVisualizer#review-details ");
            await OutputPane.Instance.WriteAsync(string.Empty);

            // Get all resource files from the solution
            // Do this now, rather than in adornment manager for performance and to avoid thread issues
            if (await this.GetServiceAsync(typeof(DTE)) is DTE dte)
            {
                var fileName = dte.Solution.FileName;

                if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                {
                    var slnDir = Path.GetDirectoryName(fileName);
                    await this.SetOrUpdateListOfResxFilesAsync(slnDir);

                    this.WatchForSolutionOrProjectChanges(fileName);
                }

                if (ResourceAdornmentManager.ResourceFiles.Any())
                {
                    var plural = ResourceAdornmentManager.ResourceFiles.Count > 1 ? "s" : string.Empty;
                    await OutputPane.Instance.WriteAsync($"String Resource Visualizer initialized with {ResourceAdornmentManager.ResourceFiles.Count} resource file{plural}.");

                    foreach (var resourceFile in ResourceAdornmentManager.ResourceFiles)
                    {
                        await OutputPane.Instance.WriteAsync(resourceFile);
                    }
                }
                else
                {
                    await OutputPane.Instance.WriteAsync($"String Resource Visualizer could not find any resource files to load.");
                }
            }
        }

        private void WatchForSolutionOrProjectChanges(string solutionFileName)
        {
            this.SlnWatcher.Filter = Path.GetFileName(solutionFileName);
            this.SlnWatcher.Path = Path.GetDirectoryName(solutionFileName);
            this.SlnWatcher.IncludeSubdirectories = false;
            this.SlnWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            this.SlnWatcher.Changed -= this.SlnWatcher_Changed;
            this.SlnWatcher.Changed += this.SlnWatcher_Changed;
            this.SlnWatcher.Renamed -= this.SlnWatcher_Renamed;
            this.SlnWatcher.Renamed += this.SlnWatcher_Renamed;
            this.SlnWatcher.EnableRaisingEvents = true;

            // Get both .csproj & .vbproj
            this.ProjWatcher.Filter = "*.*proj";
            this.ProjWatcher.Path = Path.GetDirectoryName(solutionFileName);
            this.ProjWatcher.IncludeSubdirectories = true;
            this.ProjWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            this.ProjWatcher.Changed -= this.ProjWatcher_Changed;
            this.ProjWatcher.Changed += this.ProjWatcher_Changed;
            this.ProjWatcher.Renamed -= this.ProjWatcher_Renamed;
            this.ProjWatcher.Renamed += this.ProjWatcher_Renamed;
            this.ProjWatcher.EnableRaisingEvents = true;
        }

        private async void SlnWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            await this.SetOrUpdateListOfResxFilesAsync(Path.GetDirectoryName(e.FullPath));
        }

        private async void SlnWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            // Don't want to know about temporary files created during save.
            if (e.FullPath.EndsWith(".sln"))
            {
                await this.SetOrUpdateListOfResxFilesAsync(Path.GetDirectoryName(e.FullPath));
            }
        }

        private async void ProjWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // Only interested in C# & VB.Net projects as that's all we visualize for.
            if (e.FullPath.EndsWith(".csproj") || e.FullPath.EndsWith(".vbproj"))
            {
                await this.SetOrUpdateListOfResxFilesAsync(((FileSystemWatcher)sender).Path);
            }
        }

        private async void ProjWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            // Only interested in C# & VB.Net projects as that's all we visualize for.
            if (e.FullPath.EndsWith(".csproj") || e.FullPath.EndsWith(".vbproj"))
            {
                await this.SetOrUpdateListOfResxFilesAsync(((FileSystemWatcher)sender).Path);
            }
        }

        private async Task LoadSystemTextSettingsAsync(CancellationToken cancellationToken)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            IVsFontAndColorStorage storage = (IVsFontAndColorStorage)VSPackage.GetGlobalService(typeof(IVsFontAndColorStorage));

            var guid = new Guid("A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0");

            if (storage != null && storage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS)) == Microsoft.VisualStudio.VSConstants.S_OK)
            {
                LOGFONTW[] fnt = new LOGFONTW[] { new LOGFONTW() };
                FontInfo[] info = new FontInfo[] { new FontInfo() };

                if (storage.GetFont(fnt, info) == Microsoft.VisualStudio.VSConstants.S_OK)
                {
                    var fontSize = info[0].wPointSize;

                    if (fontSize > 0)
                    {
                        ResourceAdornmentManager.TextSize = fontSize;
                    }
                }
            }

            if (storage != null && storage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_NOAUTOCOLORS | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS)) == Microsoft.VisualStudio.VSConstants.S_OK)
            {
                var info = new ColorableItemInfo[1];

                // Get the color value configured for regular string display
                if (storage.GetItem("String", info) == Microsoft.VisualStudio.VSConstants.S_OK)
                {
                    var win32Color = (int)info[0].crForeground;

                    int r = win32Color & 0x000000FF;
                    int g = (win32Color & 0x0000FF00) >> 8;
                    int b = (win32Color & 0x00FF0000) >> 16;

                    var textColor = Color.FromRgb((byte)r, (byte)g, (byte)b);

                    ResourceAdornmentManager.TextForegroundColor = textColor;
                }
            }
        }

        private async Task SetOrUpdateListOfResxFilesAsync(string slnDirectory)
        {
            await OutputPane.Instance.WriteAsync("Reloading list of resx files.");

            var allResxFiles = Directory.EnumerateFiles(slnDirectory, "*.resx", SearchOption.AllDirectories);

            var resxFilesOfInterest = new List<string>();

            foreach (var resxFile in allResxFiles)
            {
                // Only want neutral language resources, not locale specific ones
                if (!Path.GetFileNameWithoutExtension(resxFile).Contains("."))
                {
                    resxFilesOfInterest.Add(resxFile);
                }
            }

            await ResourceAdornmentManager.LoadResourcesAsync(resxFilesOfInterest, slnDirectory);
        }
    }
}

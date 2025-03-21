﻿// <copyright file="StringResVizPackage.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using EnvDTE;
using Microsoft;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using static Microsoft.VisualStudio.VSConstants;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
	[ProvideAutoLoad(UICONTEXT.CSharpProject_string, PackageAutoLoadFlags.BackgroundLoad)]
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)] // Info on this package for Help/About
	[ProvideOptionPage(typeof(OptionsGrid), Vsix.Name, "General", 0, 0, true)]
	[Guid(StringResVizPackage.PackageGuidString)]
	public sealed class StringResVizPackage : AsyncPackage
	{
		public const string PackageGuidString = "8c14dc72-9022-42ff-a85c-1cfe548a8956";

		public StringResVizPackage()
		{
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		public static StringResVizPackage Instance { get; private set; }

		public OptionsGrid Options
		{
			get
			{
				return (OptionsGrid)this.GetDialogPage(typeof(OptionsGrid));
			}
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
			SolutionEvents.OnAfterCloseSolution += this.HandleCloseSolution;

			await this.SetUpRunningDocumentTableEventsAsync(cancellationToken).ConfigureAwait(false);

			var componentModel = GetGlobalService(typeof(SComponentModel)) as IComponentModel;

			if (!ConstFinder.HasParsedSolution)
			{
				await OutputPane.Instance.WriteAsync("About to parse solution after package load.");
				await ConstFinder.TryParseSolutionAsync(componentModel);
			}

			await this.LoadSystemTextSettingsAsync(cancellationToken);

			VSColorTheme.ThemeChanged += (e) => this.LoadSystemTextSettingsAsync(CancellationToken.None).LogAndForget(nameof(StringResourceVisualizer));

			await SponsorRequestHelper.CheckIfNeedToShowAsync();

			Instance = this;

			await TrackBasicUsageAnalyticsAsync();
		}

		private static async Task TrackBasicUsageAnalyticsAsync()
		{
			try
			{
#if !DEBUG
			if (string.IsNullOrWhiteSpace(AnalyticsConfig.TelemetryConnectionString))
			{
				return;
			}

			var config = new TelemetryConfiguration
			{
				ConnectionString = AnalyticsConfig.TelemetryConnectionString,
			};

			var client = new TelemetryClient(config);

			var properties = new Dictionary<string, string>
				{
					{ "VsixVersion", Vsix.Version },
					{ "VsVersion", Microsoft.VisualStudio.Telemetry.TelemetryService.DefaultSession?.GetSharedProperty("VS.Core.ExeVersion") },
					{ "Architecture", RuntimeInformation.ProcessArchitecture.ToString() },
					{ "MsInternal", Microsoft.VisualStudio.Telemetry.TelemetryService.DefaultSession?.IsUserMicrosoftInternal.ToString() },
				};

			client.TrackEvent(Vsix.Name, properties);
#endif
			}
			catch (Exception exc)
			{
				System.Diagnostics.Debug.WriteLine(exc);
				await OutputPane.Instance.WriteAsync("Error tracking usage analytics: " + exc.Message);
			}
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

		private void HandleCloseSolution(object sender, EventArgs e)
		{
			ConstFinder.Reset();
			ResourceAdornmentManager.ClearCache();
		}

		private async Task HandleOpenSolutionAsync(CancellationToken cancellationToken)
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			await OutputPane.Instance.WriteAsync($"{Vsix.Name} v{Vsix.Version}");

			// Get all resource files from the solution
			// Do this now, rather than in adornment manager for performance and to avoid thread issues
			if (await this.GetServiceAsync(typeof(DTE)) is DTE dte)
			{
				var fileName = dte.Solution.FileName;
				string rootDir = null;

				if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
				{
					rootDir = Path.GetDirectoryName(fileName);
				}

				if (string.IsNullOrWhiteSpace(rootDir))
				{
					await OutputPane.Instance?.WriteAsync("No solution file found so attempting to load resources for project file.");

					try
					{
						fileName = ((dte.ActiveSolutionProjects as Array).GetValue(0) as EnvDTE.Project).FileName;
					}
					catch (Exception)
					{
						// Assuming this happens due to opening a csproj file rather than an sln.
						await OutputPane.Instance?.WriteAsync("No projects found in ActiveSolution so attempting to load resources for project file.");

						IVsSolution sol = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
						Assumes.Present(sol);
						sol.GetProjectFilesInSolution((uint)__VSGETPROJFILESFLAGS.GPFF_SKIPUNLOADEDPROJECTS, 0, null, out uint numProjects);
						string[] projects = new string[numProjects];
						sol.GetProjectFilesInSolution((uint)__VSGETPROJFILESFLAGS.GPFF_SKIPUNLOADEDPROJECTS, numProjects, projects, out numProjects);

						// GetProjectFilesInSolution also returns solution folders, so we want to do some filtering
						// things that don't exist on disk certainly can't be project files
						// Need to allow for opening a .cs file directly - a "virtual solution" will be created for this but it won't be part of a project.
						fileName = projects.FirstOrDefault(p => !string.IsNullOrEmpty(p) && System.IO.File.Exists(p));
					}

					if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
					{
						rootDir = Path.GetDirectoryName(fileName);
					}
				}

				if (!string.IsNullOrWhiteSpace(rootDir) && Directory.Exists(rootDir))
				{
					await this.SetOrUpdateListOfResxFilesAsync(rootDir);
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
					Messenger.ReloadResources += async () =>
					{
						try
						{
							await this.SetOrUpdateListOfResxFilesAsync(rootDir);
						}
						catch (Exception exc)
						{
							await OutputPane.Instance?.WriteAsync("Unexpected error when reloading resources.");
							await OutputPane.Instance?.WriteAsync(exc.Message);
							await OutputPane.Instance?.WriteAsync(exc.StackTrace);
						}
					};
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

					this.WatchForSolutionOrProjectChanges(fileName);
				}

				if (ResourceAdornmentManager.ResourceFiles.Any())
				{
					var plural = ResourceAdornmentManager.ResourceFiles.Count > 1 ? "s" : string.Empty;
					await OutputPane.Instance?.WriteAsync($"String Resource Visualizer initialized with {ResourceAdornmentManager.ResourceFiles.Count} resource file{plural}.");

					foreach (var resourceFile in ResourceAdornmentManager.ResourceFiles)
					{
						await OutputPane.Instance?.WriteAsync(resourceFile);
					}
				}
				else
				{
					await OutputPane.Instance?.WriteAsync("String Resource Visualizer could not find any resource files to load.");
				}
			}

			if (!ConstFinder.HasParsedSolution)
			{
				await OutputPane.Instance.WriteAsync("About to parse solution due to solution load.");
				await ConstFinder.TryParseSolutionAsync();
			}
		}

		private async Task SetUpRunningDocumentTableEventsAsync(CancellationToken cancellationToken)
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			var runningDocumentTable = new RunningDocumentTable(this);

			runningDocumentTable.Advise(MyRunningDocTableEvents.Instance);
		}

		private void WatchForSolutionOrProjectChanges(string solutionFileName)
		{
			// It might actually be the project file name if no solution file exists
			if (solutionFileName.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase))
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
			}

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

#pragma warning disable VSTHRD100 // Avoid async void methods
		private async void SlnWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			try
			{
				await this.SetOrUpdateListOfResxFilesAsync(Path.GetDirectoryName(e.FullPath));
			}
			catch (Exception exc)
			{
				await OutputPane.Instance?.WriteAsync("Unexpected error when solution changed.");
				await OutputPane.Instance?.WriteAsync(exc.Message);
				await OutputPane.Instance?.WriteAsync(exc.StackTrace);
			}
		}

		private async void SlnWatcher_Renamed(object sender, RenamedEventArgs e)
		{
			try
			{
				// Don't want to know about temporary files created during save.
				if (e.FullPath.EndsWith(".sln"))
				{
					await this.SetOrUpdateListOfResxFilesAsync(Path.GetDirectoryName(e.FullPath));
				}
			}
			catch (Exception exc)
			{
				await OutputPane.Instance?.WriteAsync("Unexpected error when solution renamed.");
				await OutputPane.Instance?.WriteAsync(exc.Message);
				await OutputPane.Instance?.WriteAsync(exc.StackTrace);
			}
		}

		private async void ProjWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			try
			{
				// Only interested in C# & VB.Net projects as that's all we visualize for.
				if (e.FullPath.EndsWith(".csproj") || e.FullPath.EndsWith(".vbproj"))
				{
					await this.SetOrUpdateListOfResxFilesAsync(((FileSystemWatcher)sender).Path);
				}
			}
			catch (Exception exc)
			{
				await OutputPane.Instance?.WriteAsync("Unexpected error when project changed.");
				await OutputPane.Instance?.WriteAsync(exc.Message);
				await OutputPane.Instance?.WriteAsync(exc.StackTrace);
			}
		}

		private async void ProjWatcher_Renamed(object sender, RenamedEventArgs e)
		{
			try
			{
				// Only interested in C# & VB.Net projects as that's all we visualize for.
				if (e.FullPath.EndsWith(".csproj") || e.FullPath.EndsWith(".vbproj"))
				{
					await this.SetOrUpdateListOfResxFilesAsync(((FileSystemWatcher)sender).Path);
				}
			}
			catch (Exception exc)
			{
				await OutputPane.Instance?.WriteAsync("Unexpected error when project renamed.");
				await OutputPane.Instance?.WriteAsync(exc.Message);
				await OutputPane.Instance?.WriteAsync(exc.StackTrace);
			}
		}
#pragma warning restore VSTHRD100 // Avoid async void methods

		private async Task LoadSystemTextSettingsAsync(CancellationToken cancellationToken)
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			IVsFontAndColorStorage storage = (IVsFontAndColorStorage)StringResVizPackage.GetGlobalService(typeof(IVsFontAndColorStorage));

			var guid = new Guid("A27B4E24-A735-4d1d-B8E7-9716E1E3D8E0");

			if (storage != null && storage.OpenCategory(ref guid, (uint)(__FCSTORAGEFLAGS.FCSF_READONLY | __FCSTORAGEFLAGS.FCSF_LOADDEFAULTS)) == Microsoft.VisualStudio.VSConstants.S_OK)
			{
#pragma warning disable SA1129 // Do not use default value type constructor
				LOGFONTW[] fnt = new LOGFONTW[] { new LOGFONTW() };
				FontInfo[] info = new FontInfo[] { new FontInfo() };
#pragma warning restore SA1129 // Do not use default value type constructor

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
			await OutputPane.Instance?.WriteAsync("Reloading list of resx files.");

			await TaskScheduler.Default;

			var sw = new System.Diagnostics.Stopwatch();
			sw.Start();

			var allResxFiles = Directory.EnumerateFiles(slnDirectory, "*.resx", SearchOption.AllDirectories);

			sw.Stop();

			if (sw.Elapsed > TimeSpan.FromSeconds(1))
			{
				await OutputPane.Instance.WriteAsync($"Enumerating files took longer than expected: {sw.ElapsedMilliseconds} milliseconds");
			}

			var resxFilesOfInterest = new List<string>();

			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

			var preferredCulture = this.Options.PreferredCulture;

			foreach (var resxFile in allResxFiles)
			{
				await Task.Yield();

				if (!Path.GetFileNameWithoutExtension(resxFile).Contains("."))
				{
					// Neutral language resources, not locale specific ones
					resxFilesOfInterest.Add(resxFile);
				}
				else if (!string.IsNullOrWhiteSpace(preferredCulture))
				{
					// Locale specific resource if specified
					if (Path.GetFileNameWithoutExtension(resxFile).EndsWith($".{preferredCulture}", StringComparison.InvariantCultureIgnoreCase))
					{
						resxFilesOfInterest.Add(resxFile);
					}
				}
			}

			await ResourceAdornmentManager.LoadResourcesAsync(resxFilesOfInterest, slnDirectory, this.Options);
		}
	}
}

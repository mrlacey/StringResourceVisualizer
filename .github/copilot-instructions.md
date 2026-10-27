# Copilot Instructions

## Project Details

* String Resource Visualizer – a Visual Studio extension (VSIX) that shows the text of a string resource (`.resx`) above the line of C#, Visual Basic or Razor code that uses it; the features are described in #file:'README.md', the history in #file:'CHANGELOG.md'
* Installs into Visual Studio 2022 from 17.10 up to, but not including, 18.0, on `amd64` and `arm64` (see #file:'src/source.extension.vsixmanifest'), so Visual Studio 2026 is not a target; .NET Framework 4.8.1; `LangVersion` is 12.0 (set in #file:'Directory.Build.props'). The Visual Studio range is written in several places that must change together: the two `InstallationTarget` elements of the manifest (one per architecture), its `CoreEditor` prerequisite, `MinimumVisualStudioVersion` (15.0) in the project file and the badges of the README, which still include one for Visual Studio 2019
* The solution is #file:'StringResourceVisualizer.slnx', in the XML `.slnx` format. It needs the .NET SDK 9.0.200 or later, or the MSBuild of Visual Studio or Build Tools 17.13 or later (`dotnet sln` edits it). It has two old-style (non-SDK) projects: the extension #file:'src/StringResourceVisualizer.csproj' and its tests #file:'src/StringResourceVisualizer.Tests/StringResourceVisualizer.Tests.csproj'. Every `.cs`, content and resource file is listed in them by hand, so a new file is neither compiled nor packaged until it is added there. Package versions are `PackageReference` items in the same files – there is no central package management, and #file:'nuget.config' maps every package to nuget.org
* Windows only: building needs Visual Studio with the "Visual Studio extension development" workload and the .NET Framework 4.8.1 targeting pack (without the pack the build fails with `MSB3644`). Use the MSBuild of Visual Studio; `dotnet build` does not build VSIX projects
* Build from the repository root in PowerShell. `msbuild` is usually not on `PATH`, so find it with `vswhere` (add `-version '[17.0,18.0)'` to build with Visual Studio 2022 instead of the latest one; the CI runs on `windows-2022`, which has Visual Studio 2022); `DeployExtension=false` keeps the build from installing the extension into the Experimental instance:

  ```powershell
  $msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  & $msbuild StringResourceVisualizer.slnx -restore -p:Configuration=Debug -p:DeployExtension=false -v:m
  ```

* Test: `src/StringResourceVisualizer.Tests` is an MSTest project with four tests of `ResourceAdornmentManager.FormatDisplayText`. Run it from Test Explorer, or after a build with the `vstest.console.exe` of Visual Studio:

  ```powershell
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -property installationPath
  & "$vs\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" src\StringResourceVisualizer.Tests\bin\Debug\StringResourceVisualizer.Tests.dll
  ```

  Everything else (package load, adornments, the options page) has no automated tests: run the extension in the Visual Studio Experimental instance (F5 starts `devenv.exe /rootsuffix Exp`, see the start options of the project) and open a solution that has a `.resx` file and code that uses it
* CI: GitHub Actions (#file:'.github/workflows/build.yaml') builds the solution in `Debug` on `windows-2022` for pushes and pull requests to `main` (changes to root-level `.md` files only are ignored), runs the tests through `josepho0918/vstest-action` and uploads the VSIX as an artifact. A step stamps the run number into the version in the manifest and in `Vsix.Version` of `src/source.extension.cs`, so change versions by hand only for a deliberate release bump. The last steps publish a test badge to a gist of the original author; the upload runs on `main` only and needs a `GIST_TOKEN` secret that a fork does not have

## Solution Structure

**IMPORTANT: Every new source file must be added to #file:'src/StringResourceVisualizer.csproj'** (or to the test project): `<Compile Include>` for code and `<Content Include>` with `IncludeInVSIX` for files that go into the VSIX. A file that is created outside Visual Studio is not added automatically.

```text
/
├── .claude/                                  – Claude Code settings (the vs-agent-plugins plugin) and helper scripts; `skills` is a local, git-ignored link to `.github/skills`
├── .github/                                  – these instructions, the `vs-extension-expert` agent, skills, prompts, issue templates and workflows (the CI build among them)
├── art/                                      – images for the README
├── src/                                      – the extension and its tests
│   ├── StringResVizPackage.cs                – the `AsyncPackage`: option page, solution events, running document table events, the list of `.resx` files and the file watchers
│   ├── ResourceAdornmentManagerFactory.cs    – MEF export that creates a `ResourceAdornmentManager` for each text view and defines the adornment layer `StringResourceCommentLayer`
│   ├── ResourceAdornmentManager.cs           – loads the `.resx` files as `XmlDocument`s, finds the usages on the lines of a text view and adds a `TextBlock` above the line
│   ├── MyLineTransformSourceProvider.cs      – MEF export of the `ILineTransformSourceProvider` that creates the `MyLineTransformSource`
│   ├── MyLineTransformSource.cs              – makes room above the lines that have an adornment
│   ├── MyRunningDocTableEvents.cs            – reloads the known constants when a document is saved or its window is shown
│   ├── ConstFinder.cs                        – collects the `const` fields of the solution through the Roslyn workspace, for the ILocalizer support
│   ├── OptionsGrid.cs                        – the Tools > Options > String Resource Visualizer page
│   ├── OutputPane.cs                         – the "String Resource Visualizer" pane of the Output window
│   ├── Messenger.cs                          – the event that asks for a reload of the resources
│   ├── Constants.cs, StringExtensions.cs, TaskExtensions.cs – helpers (`LogAndForget` is in `TaskExtensions.cs`)
│   ├── SponsorDetector.cs, SponsorRequestHelper.cs, AnalyticsConfig.cs – sponsorship message and telemetry settings of the original author (see below)
│   ├── signvsix.targets, filestosign.txt     – signing of the release build; runs only when `SM_CERT_FP_SHA256` is set
│   ├── GlobalSuppressions.cs, StringResourceVisualizer.ruleset, stylecop.json – code analysis settings
│   ├── source.extension.vsixmanifest / .cs   – the manifest and the file generated from it
│   ├── Properties/AssemblyInfo.cs            – assembly attributes; the version comes from the `Vsix` constants
│   ├── Resources/Icon.png
│   ├── App.config
│   ├── StringResourceVisualizer.csproj
│   └── StringResourceVisualizer.Tests/       – the MSTest project
├── CHANGELOG.md, CONTRIBUTING.md, LICENSE, README.md
├── Directory.Build.props                     – `LangVersion`
├── Directory.Build.targets                   – links `.claude/skills` to `.github/skills`
├── nuget.config
└── StringResourceVisualizer.slnx
```

### How It Works

* `StringResVizPackage` is an `AsyncPackage` that loads in the background when a C# project is loaded (`ProvideAutoLoad` names only that context). `InitializeAsync` switches to the UI thread, handles a solution that is already open, subscribes to `SolutionEvents.OnAfterOpenSolution` and `OnAfterCloseSolution`, advises the running document table, parses the solution for constants, reads the font size and the color of strings from the Fonts and Colors settings (again whenever the theme changes), shows the sponsorship message and sends the usage telemetry. `StringResVizPackage.Instance` is assigned near the end of `InitializeAsync`, so code that can run earlier, such as a text view that is created while the package loads, must not assume that it is set: the constructor of `ResourceAdornmentManager` loads the package through `IVsShell.LoadPackage` when `Instance` is still `null` (which is how it loads for other solutions), and `MyLineTransformSource` reads `Instance.Options` without a check.
* When a solution opens, `HandleOpenSolutionAsync` finds the solution (or project) folder and `SetOrUpdateListOfResxFilesAsync` enumerates all `.resx` files below it. A file counts as neutral-language when its name has no dot before the extension, so `Shared.Strings.resx` is skipped unless it ends in the preferred culture, which is used when one is set. `ResourceAdornmentManager.LoadResourcesAsync` loads the files as `XmlDocument`s, and `FileSystemWatcher`s on the solution, the project files and the `.resx` files reload them when they change. Closing the options page asks for a reload through `Messenger`.
* Every document text view (role `Document`) of the content types `CSharp`, `Basic`, `Razor`, `RazorCSharp` and `RazorCoreCSharp` gets a `ResourceAdornmentManager` (one per view, kept in the view's `Properties`): `ResourceAdornmentManagerFactory` creates it in `TextViewCreated`, and `MyLineTransformSourceProvider` creates it too when it is the first to run. The manager handles `LayoutChanged`: for the new or reformatted lines it looks for `<resource file name>.` (and for namespace aliases when that option is on), reads the key that follows, finds the text in the loaded documents (preferred culture first) and adds a `TextBlock` with `"text"` to the adornment layer, positioned at the usage. `FormatDisplayText` shortens a value with line breaks and marks the cut with the return symbol (U+23CE); its tests show the exact rules.
* ASP.NET Core localizer support (experimental, on by default): a line that contains one of the identifiers in the option (default `localizer[` and `loc[`) is read as `localizer["Key"]` or `localizer[SomeConst]`. A constant is resolved through `ConstFinder`, and the resource file is chosen by how closely its path resembles the code file (`GetLocalizerDocsOfInterest`).
* `MyLineTransformSource` adds the space above a line that has an adornment: the default top space, plus the top and bottom padding options scaled by `Constants.TextBlockSizeToFontScaleFactor`, plus the editor font size. The height of the `TextBlock` is the font size times the same factor plus the padding; keep the two calculations consistent when you change one. The expression for the `TextBlock` height has an operator-precedence slip (`a + b ?? 0 + c ?? 0`), so the bottom padding is never added to it.
* `ConstFinder` walks the syntax trees of every project of the `VisualStudioWorkspace` and keeps the `const` fields in `KnownConsts` as `(key, qualification, value, source)`; it yields between steps so that a large solution does not freeze the UI. `MyRunningDocTableEvents` refreshes the constants of the active document on save and when a document window is shown.
* `ResourceAdornmentManager` keeps `ResourceFiles`, `SearchValues`, `XmlDocs`, `ResourcesLoaded` and `ResxWatcher` in static members that are shared by all text views. `LoadResourcesAsync` and `ReloadResourceFileAsync` change them on background threads while the layout handler on the UI thread reads them; only the `ResourcesLoaded` flag guards that. `HandleOpenSolutionAsync` adds a new `Messenger.ReloadResources` handler each time a solution opens and nothing removes the old ones, so after several solutions, closing the options page reloads the resources for every earlier root folder. Keep both in mind before you change the loading code.
* `OutputPane.Instance` creates the pane on first use and its constructor requires the UI thread, so the first access must happen there.

### Parts of the Original Author

The publisher identity in the manifest, the sponsorship message (`SponsorDetector`, `SponsorRequestHelper`), the telemetry (`AnalyticsConfig.TelemetryConnectionString` is empty in the repository, and `TrackBasicUsageAnalyticsAsync` sends data only in non-`DEBUG` builds when it is set) and the signing (`signvsix.targets`) belong to Matt Lacey. Do not change or remove them unless the task asks for it. `SponsorDetector.IsSponsorAsync` says in a comment that the released extension is built with a different implementation.

## Visual Studio Extension Rules

* Threading: use Visual Studio COM objects (`DTE`, `IVs*` services) and WPF elements on the UI thread only. Switch with `await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken)` (`this.JoinableTaskFactory` inside the package) before you create or call such an object. Never block with `.Result` or `.Wait()`. The two callbacks of `MyRunningDocTableEvents` that call `ThreadHelper.JoinableTaskFactory.Run` are the existing exception, because `IVsRunningDocTableEvents` is synchronous – do not add more. Keep the work per line in `LayoutChangedHandler` small: it runs for every layout of every text view. The VSTHRD analyzers come with `Microsoft.VisualStudio.SDK`: fix what they report, and where a suppression is unavoidable use a narrow `#pragma warning disable` / `restore` pair, as the existing code does for `async void` handlers
* Errors: never swallow a caught exception – write it to the Output pane with `OutputPane.Instance.WriteAsync` (message, source and stack trace, as the existing handlers do), or hand a faulted task to `TaskExtensions.LogAndForget`, which writes to the activity log. Never let an exception escape from an event handler into Visual Studio: the `async void` handlers catch and log (`LayoutChangedHandler` catches only `InvalidOperationException` itself and relies on the catch-all inside `CreateVisualsAsync`). `async void` is for event handlers only. A few existing `catch` blocks only call `Debug.WriteLine` on purpose and say why in a comment; a new one needs the same justification
* Theming: the adornment takes its font size from the Fonts and Colors setting of the text editor and its color from the setting for strings (`ResourceAdornmentManager.TextSize` and `TextForegroundColor`, refreshed on `VSColorTheme.ThemeChanged`); never hard-code colors or fonts. The gray and the size 10 are only the values used until the settings are read
* Editor extensibility: the exports are MEF parts of the extension's own assembly, declared as the `MefComponent` asset in the manifest. The adornment layer name `StringResourceCommentLayer` is written twice, in the `[Name]` of the layer definition and in `view.GetAdornmentLayer(...)`, and the `[ContentType]` lists of `ResourceAdornmentManagerFactory` and `MyLineTransformSourceProvider` must stay equal; a mismatch compiles and fails or silently does nothing at run time
* Generated files: `src/source.extension.cs` carries an `<auto-generated>` header and is made from the manifest by the `VsixManifestGenerator` custom tool set in the project file, which needs the VSIX Synchronizer extension. MSBuild and agents cannot run it, so edit the file by hand and keep it in sync with `source.extension.vsixmanifest` (they are in sync now)
* Failures that appear only at run time: a missing manifest asset (such as the `MefComponent` one), a mismatched layer name or content type, or a missing `<Content>` item compile fine and break the extension later. Check such changes in the Experimental instance
* .NET Framework 4.8.1: language features that need no runtime support work (file-scoped namespaces, pattern matching, switch expressions, target-typed `new`, `is not null`); `record` and `init` need an `IsExternalInit` polyfill type, which the project does not have. The framework has no `Index`/`Range` or default interface members. `Span<T>`, `Memory<T>` and `IAsyncEnumerable<T>` compile only because the packages that the project restores bring `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`; do not add a polyfill package for something that already compiles, and check that an API exists in .NET Framework 4.8.1 before using it
* Package versions: Visual Studio loads its own copy of some libraries. The VSIX does not contain `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll` or `Microsoft.VisualStudio.LanguageServices.dll`, so the extension runs against the Roslyn of the Visual Studio that hosts it, and a Roslyn API that is newer than the oldest supported Visual Studio (17.10) fails at run time. `Microsoft.VisualStudio.SDK` is 17.10.40171, the same as the minimum Visual Studio in the manifest; keep the two aligned, and check the build warnings and run the extension before you raise a version

## Libraries in Use

* [Visual Studio SDK](https://learn.microsoft.com/visualstudio/extensibility/) (`Microsoft.VisualStudio.SDK` 17.10.40171) – the shell, text editor and threading APIs, `EnvDTE`, the interop assemblies and the VSTHRD analyzers
* `Microsoft.CodeAnalysis`, `Microsoft.CodeAnalysis.CSharp` and `Microsoft.VisualStudio.LanguageServices` 4.11.0 – the Roslyn workspace that `ConstFinder` reads
* `Microsoft.ApplicationInsights` – the usage telemetry of the release build
* `MessagePack`, `StreamJsonRpc`, `Microsoft.ServiceHub.Framework`, `Microsoft.IO.Redist` and several `System.*` packages – dependencies that the project pins explicitly
* `WarnAboutTODOs` – turns each `TODO` comment into a build warning
* MSTest with `MSTest.Analyzers` (3.6.3) in the test project

## Agents and Skills

* Use the `vs-extension-expert` agent (#file:'.github/agents/vs-extension-expert.agent.md') for work on the extension code.
* Skills live in `.github/skills`; `Directory.Build.targets` links the folder to the git-ignored `.claude/skills` for Claude Code. They are copied from [`vs-agent-plugins`](https://github.com/madskristensen/vs-agent-plugins) by Mads Kristensen, see #file:'.github/skills/README.md': `handling-async-threading`, `handling-extension-errors`, `adding-editor-adornments`, `listening-text-view-events`, `adding-options-settings` and `handling-solution-events`. When a task matches the domain of a skill, load it and follow it before you write code.
* `.claude/settings.json` also enables the whole catalogue (40 skills) as the Claude Code plugin `vs-extensibility-skills`; if Claude Code has not offered it, install it with `claude plugin install vs-extensibility-skills@vs-agent-plugins --scope project`.
* The skills describe three approaches: VisualStudio.Extensibility (out of process), Community.VisualStudio.Toolkit and VSSDK. This extension is in-process VSSDK (`AsyncPackage`) without the Toolkit, so follow the VSSDK parts and ignore the others.

## MCP Servers

Agents discover servers from #file:'.mcp.json'; these are only hints on when to prefer one.

* Use the `GitHub` server for code search in dependency repositories. In Claude Code it authenticates through `.claude/scripts/github-mcp-headers.ps1`, which reuses the GitHub token stored by Git Credential Manager, because the server does not support the OAuth dynamic client registration Claude Code needs.
* Use the `Microsoft Learn` server for official Microsoft documentation, the Visual Studio extensibility documentation included. Visual Studio bundles this server itself, so `.mcp.json` lists it for Claude Code only.
* When the solution is open in Visual Studio and an MCP server that exposes the IDE (`vs`) is connected, prefer its tools to the shell: build with it instead of resolving MSBuild, read the diagnostics from it instead of parsing the build output, and navigate with its symbol tools instead of text search.
* When adding or changing a server, keep `servers` (Visual Studio / VS Code / GitHub Copilot) and `mcpServers` (Claude Code) in sync and sorted alphabetically by key, except for the servers that Visual Studio already bundles (Microsoft Learn, NuGet): those go only in `mcpServers`.

## Definition of Done

Before reporting a task as finished:

1. Match the style of the surrounding code; the `.editorconfig` only silences the StyleCop rule `SA0001`.
2. Build the solution and make sure that it succeeds with no errors; do not claim a successful build that you did not run.
3. Compare the warnings with the build before your change: the change must add none. The build already reports warnings (the NuGet audit warnings `NU1902` and `NU1903` for `MessagePack`, `VSTHRD110` and the `TODO` comments through `WarnAboutTODOs`), so compare instead of expecting zero.
4. Run the tests when the change touches code that they cover, and add a test for logic that does not depend on Visual Studio. Say what you could not verify without the Experimental instance instead of claiming that it works: the adornments, the options page and the package load are not covered by tests.
5. Update `README.md` and `CHANGELOG.md` when a user-visible feature changes. The change log has a road map and the list of released versions; its newest entry (1.23.1) is older than the manifest version (1.23.3).

## C# Coding Guidelines

* Match the file that you edit; do not reformat code that you do not change. The conventions come from the code, because the `.editorconfig` has no style rules. Most `.cs` files are indented with 4 spaces; `StringResVizPackage.cs`, `ResourceAdornmentManager.cs` and `AnalyticsConfig.cs` use tabs, and `SponsorRequestHelper.cs` mixes both. Most `.cs` files start with a UTF-8 byte order mark, which you keep. The repository stores LF line endings (`* text=auto` in `.gitattributes`); a Windows checkout with `core.autocrlf=true` shows CRLF.
* New files start with the copyright header block of the existing files (`// <copyright file="Name.cs" company="Matt Lacey">`).
* Qualify instance members with `this.`, as the existing code does. Put `using` directives above the namespace (`System` first in most files) and remove unused ones. Several files carry `using Task = System.Threading.Tasks.Task;` from the package template; leave it where it is.
* Types live in the `StringResourceVisualizer` namespace (`StringResourceVisualizer.Tests` for the tests) in a block-scoped `namespace` declaration. Do not convert existing files.
* Name async methods with the `Async` suffix. Only the package methods take a `CancellationToken`; pass one on in new async code when the caller has one.
* XML documentation: StyleCop rules `SA1600`, `SA1611` and `SA1615` are suppressed on purpose, so documenting every member is not required, but document new public types and non-obvious members. The StyleCop rule IDs in `#pragma` lines, `GlobalSuppressions.cs` and the ruleset come from an earlier setup: the project file lists no StyleCop package.
* #file:'CONTRIBUTING.md' asks to run Code Analysis in `Debug` and to fix the findings before committing. The ruleset only lists legacy FxCop rule IDs, and the only analyzers that the project references are those of the Visual Studio SDK (VSTHRD and VSSDK), so in practice fix the analyzer warnings of the build.

## Naming Conventions

* Use PascalCase for types, methods and public members, and camelCase without a prefix for private fields (`layer`, `view`, `fileName`); `ConstFinder` uses a `_` prefix for its one private static field, so match the file.
* Prefix interface names with `I`. Name the files after the type they hold; the line transform classes are named `MyLineTransformSource` and `MyLineTransformSourceProvider`.

## General

* Make only high-confidence suggestions when reviewing code changes.
* Write code with good maintainability practices, including comments on why certain design decisions were made.
* Handle edge cases and write clear exception handling.
* Never duplicate code unless explicitly allowed.
* All comments, documentation, README files, and markdown files must be written in **English only**.
* Use an en dash (`–`) as a punctuation dash, never an em dash (`—`), and do not use emojis in text, comments or messages.
* In markdown, write each paragraph on one line instead of hard-wrapping it.
* Scripts and one-off automation: PowerShell 7 (`pwsh`, `.ps1`) or F# Interactive (`dotnet fsi`, `.fsx`); do not use Python.
* The default branch is `main`, and pull requests go against it (#file:'CONTRIBUTING.md' says `master`, which is a leftover of the template).
* Commit messages follow the history of this repository: one short sentence in the imperative, with no prefix (`Avoid crash if file opened outside of a project`). Older messages are often lower-case (`add version number to output pane`) and releases are plain version numbers (`v1.23.3`); #file:'CONTRIBUTING.md' asks for a feature prefix, a line from a template that the history does not follow.

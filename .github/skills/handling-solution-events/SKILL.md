---
name: handling-solution-events
description: React to solution and project lifecycle events in Visual Studio extensions. Use when the user asks how to detect when a solution opens, closes, loads, or unloads, how to listen for project added/removed events, how to use IVsSolutionEvents, Microsoft.VisualStudio.Shell.Events.SolutionEvents, VS.Events.SolutionEvents, or workspace notifications in a Visual Studio IDE extension. Covers VisualStudio.Extensibility (out-of-process), VSIX Community Toolkit (in-process), and legacy VSSDK (in-process) approaches.
---

# Reacting to Solution and Project Lifecycle Events in Visual Studio Extensions

Extensions frequently need to respond when the user opens, closes, or modifies a solution — for instance, to scan files, load caches, or update UI state. Visual Studio provides multiple event APIs depending on your extensibility model.

Solution events are foundational for extensions that need to initialize state when a workspace becomes available or clean up when it closes. Getting the timing wrong (e.g., enumerating projects before background load completes) leads to missing data and intermittent failures. The API landscape is broad — choosing the right one avoids unnecessary complexity.

**When to use this vs. alternatives:**
- Detect solution open/close/load to initialize or tear down extension state → **Solution events** (this skill)
- Query projects and files after the solution is loaded → [vs-solution-explorer](../interacting-solution-explorer/SKILL.md)
- React to individual file save/open/close → [vs-file-document-ops](../managing-files-documents/SKILL.md)
- React to build start/end → [vs-build-events](../handling-build-events/SKILL.md)
- Support folder-open (no solution) scenarios → [vs-open-folder](../extending-open-folder/SKILL.md)

## Decision guide

| Approach | API | Thread safety | Scope |
|----------|-----|---------------|-------|
| **VisualStudio.Extensibility** | Activation constraints + workspace queries | Fully async, out-of-process | Solution open/close via activation rules |
| **Community Toolkit** | `VS.Events.SolutionEvents` | Main thread events, simple delegates | Solution + project open/close/rename/load/unload |
| **VSSDK — Shell.Events** | `Microsoft.VisualStudio.Shell.Events.SolutionEvents` | Static events, main thread | Full lifecycle: open, close, load, unload, rename, background load complete |
| **VSSDK — IVsSolutionEvents** | `IVsSolution.AdviseSolutionEvents` | COM callback, main thread | Most granular: all solution/project events |

---

## 1. VisualStudio.Extensibility (out-of-process, recommended)

The out-of-process model doesn't use traditional event subscriptions. Instead, you use **activation constraints** to conditionally activate extension components when a solution is open, and the **Workspace** API to query solution state.

**NuGet package:** `Microsoft.VisualStudio.Extensibility`
**Key namespace:** `Microsoft.VisualStudio.Extensibility`

### Activate a command only when a solution is loaded

```csharp
[VisualStudioContribution]
internal class AnalyzeCommand : Command
{
    public override CommandConfiguration CommandConfiguration => new("Analyze Solution")
    {
        Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
        // Only visible/enabled when a solution is fully loaded
        EnabledWhen = ActivationConstraint.SolutionState(SolutionState.FullyLoaded),
    };

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
    {
        // Safe to access workspace here — solution is guaranteed to be loaded
    }
}
```

### Activation constraint options for solution state

| Constraint | Fires when |
|-----------|-----------|
| `SolutionState.Exists` | A solution is open (projects may still be loading) |
| `SolutionState.FullyLoaded` | Solution and all projects are fully loaded |
| `SolutionState.HasSingleProject` | Exactly one project in the solution |
| `SolutionState.HasMultipleProjects` | Two or more projects |
| `SolutionState.Empty` | Solution is open but contains no projects |

### React to document/file events as a proxy for solution changes

For more dynamic scenarios, combine `IDocumentEventsListener` (see vs-file-document-ops skill) with workspace queries to detect meaningful changes.

> **Note:** The VisualStudio.Extensibility model does not yet expose direct solution lifecycle event subscriptions (OnAfterOpenSolution, etc.). If your extension needs fine-grained solution events, use the in-process model or a hybrid (in-proc package + out-of-proc extensibility).

---

## 2. VSIX Community Toolkit (in-process)

The Community Toolkit provides `VS.Events.SolutionEvents` with simple .NET event delegates. This is the easiest API for reacting to solution lifecycle changes.

**NuGet package:** `Community.VisualStudio.Toolkit`
**Key namespace:** `Community.VisualStudio.Toolkit`

### Subscribe to solution events

```csharp
public sealed class MyExtensionPackage : ToolkitPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await this.RegisterCommandsAsync();

        // Solution-level events
        VS.Events.SolutionEvents.OnAfterOpenSolution += OnSolutionOpened;
        VS.Events.SolutionEvents.OnAfterCloseSolution += OnSolutionClosed;
        VS.Events.SolutionEvents.OnBeforeCloseSolution += OnBeforeSolutionClose;

        // Project-level events
        VS.Events.SolutionEvents.OnAfterOpenProject += OnProjectOpened;
        VS.Events.SolutionEvents.OnBeforeCloseProject += OnProjectClosing;
        VS.Events.SolutionEvents.OnAfterRenameProject += OnProjectRenamed;
        VS.Events.SolutionEvents.OnAfterLoadProject += OnProjectLoaded;
        VS.Events.SolutionEvents.OnBeforeUnloadProject += OnProjectUnloading;
    }

    private void OnSolutionOpened(Solution solution)
    {
        // Solution is open — safe to enumerate projects
    }

    private void OnSolutionClosed()
    {
        // Solution has been closed — clean up caches, state, etc.
    }

    private void OnBeforeSolutionClose()
    {
        // Solution is about to close — save extension state
    }

    private void OnProjectOpened(Project project)
    {
        // A project was opened/added to the solution
        string name = project?.Name;
    }

    private void OnProjectClosing(Project project)
    {
        // A project is about to be removed/closed
    }

    private void OnProjectRenamed(Project project)
    {
        // A project was renamed
    }

    private void OnProjectLoaded(Project project)
    {
        // A previously unloaded project was loaded
    }

    private void OnProjectUnloading(Project project)
    {
        // A project is about to be unloaded (not removed from solution)
    }
}
```

### Check solution state

```csharp
// Is a solution currently open?
bool isOpen = await VS.Solutions.IsOpenAsync();

// Is a solution in the process of opening?
bool isOpening = await VS.Solutions.IsOpeningAsync();

// Get all projects in the current solution
var projects = await VS.Solutions.GetAllProjectsAsync();
```

### Get all projects

```csharp
var projects = await VS.Solutions.GetAllProjectsAsync();
foreach (Project project in projects)
{
    string name = project.Name;
    string path = project.FullPath;
}
```

---

## 3a. VSSDK — Microsoft.VisualStudio.Shell.Events.SolutionEvents (recommended VSSDK approach)

`Microsoft.VisualStudio.Shell.Events.SolutionEvents` is a managed-friendly wrapper around the raw COM `IVsSolutionEvents` interfaces. It provides static events, which are simpler to use than implementing `IVsSolutionEvents` yourself. It wraps `IVsSolutionEvents` through `IVsSolutionEvents8` and `IVsSolutionLoadEvents`.

**NuGet package:** `Microsoft.VisualStudio.Shell.15.0` (part of `Microsoft.VisualStudio.SDK`)
**Key namespace:** `Microsoft.VisualStudio.Shell.Events`

### Subscribe to events

```csharp
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid("YOUR-PACKAGE-GUID")]
public sealed class MyExtensionPackage : AsyncPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Solution-level events
        SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
        SolutionEvents.OnBeforeCloseSolution += OnBeforeCloseSolution;
        SolutionEvents.OnAfterCloseSolution += OnAfterCloseSolution;

        // Background solution load events
        SolutionEvents.OnAfterBackgroundSolutionLoadComplete += OnFullyLoaded;
        SolutionEvents.OnBeforeBackgroundSolutionLoadBegins += OnBackgroundLoadStarting;

        // Project-level events
        SolutionEvents.OnAfterOpenProject += OnAfterOpenProject;
        SolutionEvents.OnBeforeCloseProject += OnBeforeCloseProject;
        SolutionEvents.OnAfterLoadProject += OnAfterLoadProject;
        SolutionEvents.OnBeforeUnloadProject += OnBeforeUnloadProject;
        SolutionEvents.OnAfterRenameProject += OnAfterRenameProject;

        // Folder events (Open Folder mode, not solution)
        SolutionEvents.OnAfterOpenFolder += OnAfterOpenFolder;
        SolutionEvents.OnAfterCloseFolder += OnAfterCloseFolder;
    }

    private void OnAfterOpenSolution(object sender, OpenSolutionEventArgs e)
    {
        // Solution has been opened
        // e.IsNewSolution: true if the solution was just created (not loaded from disk)
    }

    private void OnBeforeCloseSolution(object sender, EventArgs e)
    {
        // Solution is about to close — persist any cached state
    }

    private void OnAfterCloseSolution(object sender, EventArgs e)
    {
        // Solution is fully closed — clean up
    }

    private void OnFullyLoaded(object sender, EventArgs e)
    {
        // All projects have finished loading (including background loading)
        // This is the safest point to enumerate all projects
    }

    private void OnBackgroundLoadStarting(object sender, EventArgs e)
    {
        // A batch of projects is about to start loading in the background
    }

    private void OnAfterOpenProject(object sender, OpenProjectEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        // e.Hierarchy: IVsHierarchy for the opened project
        // e.IsAdded: true if the project was just added (not already in the solution)
    }

    private void OnBeforeCloseProject(object sender, CloseProjectEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        // e.Hierarchy: IVsHierarchy for the project about to close
        // e.IsRemoved: true if the project is being removed from the solution
    }

    private void OnAfterLoadProject(object sender, LoadProjectEventArgs e)
    {
        // A previously unloaded project was loaded
    }

    private void OnBeforeUnloadProject(object sender, UnloadProjectEventArgs e)
    {
        // A project is about to be unloaded (still in solution, just not loaded)
    }

    private void OnAfterRenameProject(object sender, HierarchyEventArgs e)
    {
        // A project was renamed
    }

    private void OnAfterOpenFolder(object sender, FolderEventArgs e)
    {
        // A folder was opened (Open Folder mode, not a .sln)
        string folderPath = e.FolderPath;
    }

    private void OnAfterCloseFolder(object sender, FolderEventArgs e)
    {
        // A folder was closed
    }
}
```

### Available events on SolutionEvents

| Event | When it fires |
|-------|---------------|
| `OnBeforeOpenSolution` | Before the solution file is opened |
| `OnAfterOpenSolution` | After the solution is opened (projects may still be loading) |
| `OnBeforeCloseSolution` | Before the solution begins closing |
| `OnAfterCloseSolution` | After the solution is fully closed |
| `OnAfterBackgroundSolutionLoadComplete` | After all projects finish loading (including background) |
| `OnBeforeBackgroundSolutionLoadBegins` | Before background project loading starts |
| `OnAfterOpenProject` | After a project is opened or added |
| `OnBeforeCloseProject` | Before a project is removed or closed |
| `OnAfterLoadProject` | After a previously unloaded project is loaded |
| `OnBeforeUnloadProject` | Before a project is unloaded |
| `OnAfterRenameProject` | After a project is renamed |
| `OnAfterRenameSolution` | After the solution file is renamed |
| `OnAfterMergeSolution` | After another solution is merged in |
| `OnAfterOpenFolder` | After opening a folder (Open Folder mode) |
| `OnAfterCloseFolder` | After closing a folder |
| `OnAfterLoadAllDeferredProjects` | After all deferred/lazy-loaded projects finish loading |
| `OnQueryCloseSolution` | Query — can observe (not cancel from managed code) |
| `OnQueryCloseProject` | Query for a project close |
| `OnQueryUnloadProject` | Query for a project unload |

---

## 3b. VSSDK — IVsSolutionEvents (lowest-level)

Implement `IVsSolutionEvents` directly for maximum control. This is the most verbose but also the most flexible — you can implement multiple levels (`IVsSolutionEvents` through `IVsSolutionEvents8`).

**NuGet package:** `Microsoft.VisualStudio.SDK`
**Key namespaces:** `Microsoft.VisualStudio.Shell`, `Microsoft.VisualStudio.Shell.Interop`

### Implement and register the event sink

```csharp
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

internal class SolutionEventListener : IVsSolutionEvents, IDisposable
{
    private readonly IVsSolution _solution;
    private readonly uint _cookie;

    public SolutionEventListener()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
        _solution.AdviseSolutionEvents(this, out _cookie);
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_cookie != 0)
            _solution.UnadviseSolutionEvents(_cookie);
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        // Solution opened. fNewSolution != 0 means it was just created.
        return VSConstants.S_OK;
    }

    public int OnAfterCloseSolution(object pUnkReserved)
    {
        // Solution fully closed
        return VSConstants.S_OK;
    }

    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
    {
        // Project opened. fAdded != 0 means it was newly added.
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
    {
        // Project about to close. fRemoved != 0 means it's being removed.
        return VSConstants.S_OK;
    }

    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
    {
        // Set pfCancel = 1 to prevent the project from closing
        return VSConstants.S_OK;
    }

    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
    {
        // Set pfCancel = 1 to prevent the solution from closing
        return VSConstants.S_OK;
    }

    // Remaining required interface members:
    public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;
    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;
    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;
    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;
}
```

### Initialize the listener from the package

```csharp
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid("YOUR-PACKAGE-GUID")]
public sealed class MyExtensionPackage : AsyncPackage
{
    private SolutionEventListener _solutionListener;

    protected override async Task InitializeAsync(
        CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _solutionListener = new SolutionEventListener();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _solutionListener?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

---

## Common patterns

### Run initialization after the solution is fully loaded

This is a very common need — wait until all projects are available before scanning:

**Community Toolkit:**

```csharp
VS.Events.SolutionEvents.OnAfterOpenSolution += async (solution) =>
{
    // Solution is open, but projects may still be loading
    // If you need all projects loaded, also wait for:
    var projects = await VS.Solutions.GetAllProjectsAsync();
    await InitializeExtensionDataAsync(projects);
};
```

**Shell.Events.SolutionEvents:**

```csharp
SolutionEvents.OnAfterBackgroundSolutionLoadComplete += (sender, e) =>
{
    // All projects are now fully loaded — safest point to scan
    ThreadHelper.ThrowIfNotOnUIThread();
    ScanAllProjects();
};
```

### Get project information from IVsHierarchy

When working with raw `IVsSolutionEvents`, the event provides `IVsHierarchy`. To get useful information:

```csharp
private void OnAfterOpenProject(IVsHierarchy hierarchy)
{
    ThreadHelper.ThrowIfNotOnUIThread();

    // Get the project name
    hierarchy.GetProperty(
        (uint)VSConstants.VSITEMID.Root,
        (int)__VSHPROPID.VSHPROPID_Name,
        out object nameObj);
    string projectName = nameObj as string;

    // Get the project file path
    hierarchy.GetCanonicalName((uint)VSConstants.VSITEMID.Root, out string projectPath);
}
```

---

## Key guidance

- **New extensions** → use VisualStudio.Extensibility activation constraints (`SolutionState.FullyLoaded`) for command visibility. For fine-grained events, hybrid in-process mode is needed.
- **Existing Toolkit extensions** → use `VS.Events.SolutionEvents` for the simplest event subscription. It covers the most common scenarios.
- **VSSDK — `Microsoft.VisualStudio.Shell.Events.SolutionEvents`** → use for the richest event set with static events. This is the recommended VSSDK approach (simpler than implementing `IVsSolutionEvents` manually). Includes background load completion and folder open/close events.
- **VSSDK — `IVsSolutionEvents`** → implement directly only when you need to **cancel** operations via `OnQueryCloseSolution`/`OnQueryCloseProject`, which is not possible with the static event wrappers.
- Always use `OnAfterBackgroundSolutionLoadComplete` (not `OnAfterOpenSolution`) if you need all projects to be fully loaded before doing work.
- Always unsubscribe from events or call `UnadviseSolutionEvents` when your extension is disposed to prevent memory leaks.
- Solution events fire on the **main thread** — keep handlers fast and offload heavy work to background tasks.

## What NOT to do

> **Do NOT** enumerate all projects in `OnAfterOpenSolution` — background-loaded projects may still be loading. Use `OnAfterBackgroundSolutionLoadComplete` instead.

> **Do NOT** do heavy work in solution event handlers — they run on the UI thread. Offload with `JoinableTaskFactory.RunAsync` or `Task.Run`; capture event data first.

> **Do NOT** forget to unsubscribe or call `UnadviseSolutionEvents` in `Dispose` — leaked subscriptions cause memory leaks and stale-reference exceptions.

> **Do NOT** implement `IVsSolutionEvents` directly when you only need basic open/close/rename — use `VS.Events.SolutionEvents` (Toolkit) or `Microsoft.VisualStudio.Shell.Events.SolutionEvents` (VSSDK). Only implement the interface when you need to cancel operations.

> **Do NOT** use `EnvDTE.Events.SolutionEvents` — requires storing a strong field reference (or COM GC stops events), and the DTE model is deprecated.

## Troubleshooting

- **Event handler never fires:** For Toolkit, verify you're subscribing in `InitializeAsync` *after* `await JoinableTaskFactory.SwitchToMainThreadAsync()`. For VSSDK, confirm `AdviseSolutionEvents` returned a non-zero cookie.
- **Projects appear empty when accessed in `OnAfterOpenSolution`:** Background-loaded projects aren't ready yet. Use `OnAfterBackgroundSolutionLoadComplete` to ensure all projects are fully loaded.
- **Events fire multiple times per solution open:** You're subscribing in a code path that runs more than once (e.g., command execution). Move subscription to `InitializeAsync` or a one-time initialization path.
- **Memory leak after repeated solution open/close cycles:** Ensure you call `UnadviseSolutionEvents` in `Dispose()` (VSSDK) or unsubscribe from static events (Toolkit/VSSDK Shell.Events).
- **Open Folder events don't fire:** Solution events don't fire for Open Folder mode. Use `IVsSolutionEvents7.OnAfterOpenFolder` or the Open Folder UI context.

## See also

- [vs-solution-explorer](../interacting-solution-explorer/SKILL.md)
- [vs-build-events](../handling-build-events/SKILL.md)
- [vs-file-document-ops](../managing-files-documents/SKILL.md)
- [vs-open-folder](../extending-open-folder/SKILL.md)
- [vs-background-tasks-progress](../showing-background-progress/SKILL.md)

## References

- [SolutionEvents class (Microsoft.VisualStudio.Shell.Events)](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.events.solutionevents)
- [IVsSolutionEvents interface](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.shell.interop.ivssolutionevents)
- [Solutions (Community Toolkit tips)](https://learn.microsoft.com/visualstudio/extensibility/vsix/tips/solutions)
- [Activation Constraints (VisualStudio.Extensibility)](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/inside-the-sdk/activation-constraints)

---
name: handling-async-threading
description: Correctly handle async/await patterns, thread switching, and JoinableTaskFactory usage in Visual Studio extensions. Use when the user asks about async/await in VS extensions, switching threads, avoiding deadlocks, using ThreadHelper, JoinableTaskFactory, fire-and-forget patterns, blocking the UI thread, VSTHRD analyzer rules, or any threading-related question in Visual Studio extension development. Covers VisualStudio.Extensibility (out-of-process), VSIX Community Toolkit (in-process), and legacy VSSDK (in-process) approaches.
---

# Async/Await and Threading in Visual Studio Extensions

Visual Studio is a single-threaded apartment (STA) application. Many services and COM objects require the **main (UI) thread**. Incorrect threading causes deadlocks, UI freezes, or crashes. This skill covers the correct, non-blocking patterns for all three extensibility approaches.

Threading is a cross-cutting concern that affects every part of an extension. The core problem: VS COM objects must be accessed from the UI thread, but blocking that thread freezes the entire IDE. `JoinableTaskFactory` solves the deadlock that occurs when the UI thread synchronously waits for background work that itself needs the UI thread.

**When this skill applies vs. alternatives:**
- Any extension code that calls `IVs*` COM interfaces or accesses VS services → **this skill** (thread switching)
- Showing progress during long operations → combine with [vs-background-tasks-progress](../showing-background-progress/SKILL.md)
- Error handling in async code (catching `OperationCanceledException`, logging) → combine with [vs-error-handling](../handling-extension-errors/SKILL.md)
- Responding to solution/project load events asynchronously → combine with [vs-solution-events](../handling-solution-events/SKILL.md)

---

## Threading Analyzer — install first

Always install the **Microsoft.VisualStudio.Threading.Analyzers** NuGet package in every VS extension project. It catches threading mistakes at compile time. It's included with the VSIX Community Toolkit.

**NuGet package:** `Microsoft.VisualStudio.Threading.Analyzers`

### Key analyzer rules

| Rule | Severity | Summary |
|------|----------|---------|
| **VSTHRD001** | Critical | Avoid legacy thread switching (`Dispatcher.Invoke`, `ThreadHelper.Generic.Invoke`). Use `JoinableTaskFactory.SwitchToMainThreadAsync` instead. |
| **VSTHRD002** | Critical | Avoid `.Wait()`, `.Result`, `.GetAwaiter().GetResult()` on tasks. Use `await` or `JoinableTaskFactory.Run`. |
| **VSTHRD003** | Critical | Avoid awaiting "foreign" tasks (tasks created outside the current `JoinableTaskFactory.Run` delegate). Wrap with `JoinableTaskFactory.RunAsync` and store as `JoinableTask`. |
| **VSTHRD004** | Critical | Always `await` the result of `SwitchToMainThreadAsync()`. Never call it without `await`. |
| **VSTHRD010** | Critical | Call `ThreadHelper.ThrowIfNotOnUIThread()` or `SwitchToMainThreadAsync()` before invoking single-threaded types (`IVs*` interfaces). |
| **VSTHRD011** | Critical | Use `AsyncLazy<T>` instead of `Lazy<T>` for lazy async initialization. Pass a `JoinableTaskFactory` to the constructor. |
| **VSTHRD100** | Advisory | Avoid `async void` methods — they crash the process on unhandled exceptions. Return `Task` instead. |
| **VSTHRD101** | Advisory | Avoid unsupported async delegates (e.g., `async void` lambdas). |
| **VSTHRD103** | Advisory | Call async method overloads when inside an async method (e.g., `ReadAsync` instead of `Read`). |
| **VSTHRD104** | Advisory | Expose an async option for public APIs that must do async work. |
| **VSTHRD110** | Advisory | Always observe `Task` results — await them, wrap with `JoinableTaskFactory.RunAsync`, or call `.Forget()`. |
| **VSTHRD200** | Guideline | Use `Async` suffix on async method names. |

---

## 1. VisualStudio.Extensibility (out-of-process, recommended)

Out-of-process extensions run in a **separate process** from Visual Studio. They communicate with the IDE over RPC. This means:

- There is **no UI thread** in your extension process — your code runs on thread-pool threads.
- There is **no `ThreadHelper`** or `JoinableTaskFactory` from `Microsoft.VisualStudio.Shell`.
- All SDK APIs are **natively async** (`ExecuteCommandAsync`, `InitializeAsync`, etc.) and return `Task`.
- You do **not** need `SwitchToMainThreadAsync` — the SDK handles thread marshaling for you.

**NuGet package:** `Microsoft.VisualStudio.Extensibility`

### Async command execution

```csharp
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

[VisualStudioContribution]
public class MyCommand : Command
{
    public override CommandConfiguration CommandConfiguration => new("%MyCommand.DisplayName%");

    public MyCommand(VisualStudioExtensibility extensibility)
        : base(extensibility)
    {
    }

    // This runs on a background thread — no UI thread concerns.
    // Always honor the cancellationToken.
    public override async Task ExecuteCommandAsync(
        IClientContext context, CancellationToken cancellationToken)
    {
        // Do async work directly — no JTF wrapping needed
        var result = await DoExpensiveWorkAsync(cancellationToken);

        // Interact with VS through the Extensibility object (all async, all safe)
        await this.Extensibility.Shell().ShowPromptAsync(
            $"Result: {result}",
            PromptOptions.OK,
            cancellationToken);
    }
}
```

### Background work with cancellation

```csharp
public override async Task ExecuteCommandAsync(
    IClientContext context, CancellationToken cancellationToken)
{
    // Offload CPU-intensive work — already on thread pool, but Task.Run
    // keeps the async state machine off the hot path
    var data = await Task.Run(() => ComputeData(), cancellationToken);

    // Always check cancellation between steps
    cancellationToken.ThrowIfCancellationRequested();

    await this.Extensibility.Shell().ShowPromptAsync(
        data.Summary, PromptOptions.OK, cancellationToken);
}
```

### Key points for VisualStudio.Extensibility

- All APIs are `async Task`-returning — just use normal `async`/`await`.
- Always pass `CancellationToken` through the call chain.
- No `ThreadHelper`, no `JoinableTaskFactory`, no `SwitchToMainThreadAsync`.
- Do **not** use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — these can cause thread-pool starvation.
- Do **not** use `async void` — return `Task` from all async methods.

---

## 2. VSIX Community Toolkit (in-process)

The Community Toolkit runs in-process and wraps VSSDK. You still need `JoinableTaskFactory` for thread switching, but the toolkit provides convenience helpers.

**NuGet packages:** `Community.VisualStudio.Toolkit`, `Microsoft.VisualStudio.Threading.Analyzers`
**Key namespaces:** `Community.VisualStudio.Toolkit`, `Microsoft.VisualStudio.Shell`, `Microsoft.VisualStudio.Threading`

### Async command — basic pattern

```csharp
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

[Command(PackageIds.MyCommand)]
internal sealed class MyCommand : BaseCommand<MyCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        // You start on the UI thread in command handlers.
        // Do quick UI work here.

        // Offload heavy work to background thread
        var result = await Task.Run(() => AnalyzeData());

        // Back on the UI thread after await (SynchronizationContext preserved).
        await VS.StatusBar.ShowMessageAsync($"Done: {result}");
    }
}
```

### Switching to the UI thread

```csharp
protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
{
    // Explicitly switch to background
    await TaskScheduler.Default;

    // Do background work
    var data = await LoadDataAsync();

    // Switch back to UI thread to update UI or call IVs* interfaces
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    // Safe to call COM / IVs* objects here
    var solution = await VS.Solutions.GetCurrentSolutionAsync();
}
```

### AsyncPackage initialization

```csharp
public sealed class MyExtensionPackage : ToolkitPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        // Background thread — do heavy init here
        await LoadConfigurationAsync();

        // Switch to UI thread when registering commands/services
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await this.RegisterCommandsAsync();
    }
}
```

### Fire-and-forget — the correct way

Never use `async void`. Use `JoinableTaskFactory.RunAsync` with `.FireAndForget()`:

```csharp
// WRONG — async void will crash on exceptions
async void OnSomethingHappened(object sender, EventArgs e)
{
    await DoWorkAsync(); // If this throws, the process crashes
}

// CORRECT — fire and forget with error reporting
void OnSomethingHappened(object sender, EventArgs e)
{
    _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
    {
        await DoWorkAsync();
    });
}

// CORRECT — for event handler registration
myObj.SomeEvent += (s, e) =>
    ThreadHelper.JoinableTaskFactory.RunAsync(() => HandleEventAsync(s, e));

private async Task HandleEventAsync(object sender, EventArgs e)
{
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    // Do work
}
```

### Calling async code from synchronous methods

When a synchronous method must call async code, use `JoinableTaskFactory.Run`:

```csharp
// This blocks the calling thread but avoids deadlocks by pumping messages
void MySyncMethod()
{
    ThreadHelper.JoinableTaskFactory.Run(async () =>
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var solution = await VS.Solutions.GetCurrentSolutionAsync();
        // ...
    });
}
```

### Ensure UI thread before calling COM/IVs* objects (VSTHRD010)

```csharp
// In a synchronous method — assert you're on the UI thread
void DoUiWork()
{
    ThreadHelper.ThrowIfNotOnUIThread();
    // Safe to use IVs* interfaces
}

// In an async method — switch to the UI thread
async Task DoUiWorkAsync()
{
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    // Safe to use IVs* interfaces
}
```

---

## 3. VSSDK (in-process, legacy)

With raw VSSDK, you manage threads manually using `JoinableTaskFactory` from `ThreadHelper` or your `AsyncPackage`.

**NuGet packages:** `Microsoft.VisualStudio.SDK`, `Microsoft.VisualStudio.Threading.Analyzers`
**Key namespaces:** `Microsoft.VisualStudio.Shell`, `Microsoft.VisualStudio.Threading`

### Switch from UI thread to background thread

```csharp
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

async Task DoWorkAsync()
{
    // Option 1: Use Task.Run for CPU-bound work
    var result = await Task.Run(() =>
    {
        // On a thread-pool thread now
        return ExpensiveComputation();
    });

    // Option 2: Explicitly yield to background via TaskScheduler.Default
    await TaskScheduler.Default;
    // On a thread-pool thread now
    DoSomethingSynchronous();
}
```

### Switch from background thread to UI thread

```csharp
async Task UpdateUiAsync()
{
    // Switch to UI thread — always await this
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    // Now on the UI thread — safe to touch IVs* and COM objects
    IVsSolution solution = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));
    solution.GetProperty((int)__VSPROPID.VSPROPID_SolutionFileName, out object value);
}
```

### Switch from background to UI thread in a synchronous method

```csharp
void SyncMethodThatNeedsUiThread()
{
    ThreadHelper.JoinableTaskFactory.Run(async delegate
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        // On the UI thread — do work
    });
}
```

### Call async code from synchronous code (VSTHRD002)

```csharp
// WRONG — causes deadlocks
void BadMethod()
{
    var result = SomeOperationAsync().Result; // VSTHRD002
    SomeOperationAsync().Wait();              // VSTHRD002
    SomeOperationAsync().GetAwaiter().GetResult(); // VSTHRD002
}

// CORRECT — Use JoinableTaskFactory.Run
void CorrectMethod()
{
    ThreadHelper.JoinableTaskFactory.Run(async delegate
    {
        var result = await SomeOperationAsync();
        // Use result
    });
}
```

### AsyncPackage initialization

```csharp
using System;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid("YOUR-PACKAGE-GUID")]
public sealed class MyPackage : AsyncPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        // This runs on a background thread.
        // Do heavy initialization work here.

        // Switch to UI thread only when needed
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Register services, commands, etc. that need the UI thread
    }
}
```

### Fire-and-forget with proper tracking (VSTHRD100, VSTHRD110)

```csharp
// WRONG — async void crashes on exceptions
async void StartBackgroundWork() // VSTHRD100
{
    await DoSomethingAsync();
}

// WRONG — unobserved task (VSTHRD110)
void StartBackgroundWork()
{
    DoSomethingAsync(); // Warning: result not observed
}

// CORRECT — fire and forget tracked by JoinableTaskFactory
void StartBackgroundWork()
{
    this.JoinableTaskFactory.RunAsync(async delegate
    {
        await Task.Yield(); // Get off the caller's callstack
        await DoSomethingAsync();
    });
}
```

### Ensure async work completes before package disposal

```csharp
public sealed class MyPackage : AsyncPackage
{
    protected override async Task InitializeAsync(
        CancellationToken cancellationToken,
        IProgress<ServiceProgressData> progress)
    {
        // Start long-running work using the package's JoinableTaskFactory.
        // AsyncPackage ensures all JoinableTask work completes before shutdown.
        JoinableTaskFactory.RunAsync(async delegate
        {
            await SomeLongRunningWorkAsync(DisposalToken);
        });
    }
}
```

The `AsyncPackage.DisposalToken` is signaled when the package is being disposed. Always honor it in long-running work.

### Lazy async initialization with AsyncLazy (VSTHRD011)

```csharp
using Microsoft.VisualStudio.Threading;

// WRONG — Lazy<Task<T>> can deadlock (VSTHRD011)
private readonly Lazy<Task<MyService>> _service = new(CreateServiceAsync);

// CORRECT — AsyncLazy<T> integrates with JoinableTaskFactory
private readonly AsyncLazy<MyService> _service;

public MyPackage()
{
    _service = new AsyncLazy<MyService>(
        CreateServiceAsync,
        ThreadHelper.JoinableTaskFactory);
}

private static async Task<MyService> CreateServiceAsync()
{
    await TaskScheduler.Default; // Move off UI thread
    return new MyService();
}

// Usage in async code:
async Task UseServiceAsync()
{
    var svc = await _service.GetValueAsync();
    svc.DoWork();
}
```

### Cancellation with SwitchToMainThreadAsync

```csharp
async Task DoWorkAsync(CancellationToken cancellationToken)
{
    // Pass cancellationToken to cancel the switch if the operation
    // is no longer needed — avoids unnecessary UI thread transitions
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

    // If you need to guarantee the next code doesn't run after cancellation:
    cancellationToken.ThrowIfCancellationRequested();

    // Do UI-thread work
}
```

### StartOnIdle — run UI work without blocking user input

Use `ThreadHelper.JoinableTaskFactory.StartOnIdle` when you need the **UI thread** but the work is **low priority** and should not delay typing, scrolling, or other user interactions. It schedules your delegate to run on the UI thread only when it is **idle** (no pending user input).

**When to use StartOnIdle:**

- Deferred UI initialization after package load (e.g., populating tool window content, setting up status bar)
- Background-triggered UI updates that aren't time-critical (e.g., refreshing decorations, updating a panel after a build)
- Any work that must touch UI-thread-affinitized objects but can wait until the user isn't actively doing something

**When NOT to use StartOnIdle:**

- Work that must complete before returning to the caller — use `SwitchToMainThreadAsync()` or `JoinableTaskFactory.Run` instead
- CPU-bound work — move that to a background thread with `Task.Run` or `await TaskScheduler.Default`, then switch to UI only for the final update
- Work that needs to happen immediately in response to a user action (e.g., command execution) — use `SwitchToMainThreadAsync()` directly

```csharp
// Deferred UI initialization — runs on UI thread when idle
await ThreadHelper.JoinableTaskFactory.StartOnIdle(async delegate
{
    for (int i = 0; i < items.Count; i++)
    {
        UpdateUIForItem(items[i]);

        // Yield frequently so user input is never delayed.
        // Each iteration will wait for the next idle slot.
        await Task.Yield();
    }
});
```

**Batching work with StartOnIdle and yielding:**

When processing many items on the UI thread, call `await Task.Yield()` between batches. Each yield returns control to the VS message pump, and `StartOnIdle` resumes your delegate only when the UI thread is idle again. This keeps the IDE responsive.

```csharp
// Update a large list of decorations without blocking the editor
_ = ThreadHelper.JoinableTaskFactory.StartOnIdle(async delegate
{
    foreach (var decoration in pendingDecorations)
    {
        ApplyDecoration(decoration); // UI-thread work
        await Task.Yield();          // let VS handle any pending user input
    }
});
```

**StartOnIdle returns a `JoinableTask`** — you can `await` it if you need to know when the idle work finishes, or discard it with `_` for true fire-and-forget.

```csharp
// Wait for idle work to complete before proceeding
JoinableTask idleWork = ThreadHelper.JoinableTaskFactory.StartOnIdle(async delegate
{
    await InitializeToolWindowContentAsync();
});

// Later, if you need the result before continuing:
await idleWork;
```

**Specifying priority with `WithPriority`:**

For finer control over scheduling priority beyond idle, combine `WithPriority` with `RunAsync`:

```csharp
using System.Windows.Threading;

// Schedule work at DataBind priority (lower than user input, higher than idle)
var lowPriorityJtf = ThreadHelper.JoinableTaskFactory
    .WithPriority(Dispatcher.CurrentDispatcher, DispatcherPriority.DataBind);

await lowPriorityJtf.RunAsync(async delegate
{
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
    RefreshTreeView();
});
```

### Wait dialog for long blocking work

```csharp
ThreadHelper.JoinableTaskFactory.Run(
    "Processing data...",
    async (progress, cancellationToken) =>
    {
        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItemAsync(items[i]);
            progress.Report(new ThreadedWaitDialogProgressData(
                $"Processing item {i + 1} of {items.Count}",
                isCancelable: true));
        }
    });
```

---

## ConfigureAwait(false) — do NOT use in VS extensions

For Visual Studio extension code, **do not use `.ConfigureAwait(false)`**. Unlike general .NET library guidance:

- `JoinableTaskFactory.Run` sets a special `SynchronizationContext` that routes continuations back to the blocked thread without deadlocking. `.ConfigureAwait(false)` defeats this optimization.
- `.ConfigureAwait(false)` causes continuations to run on thread-pool threads, consuming extra threads while the original thread blocks — leading to **thread-pool starvation**.
- Instead of `.ConfigureAwait(false)`, explicitly switch threads when you need background execution:

```csharp
// WRONG for VS extensions
var data = await LoadDataAsync().ConfigureAwait(false);

// CORRECT — explicit thread switch
await TaskScheduler.Default; // switch to background
var data = await LoadDataAsync();
```

---

## Common anti-patterns and fixes

### Anti-pattern: Blocking on async from UI thread

```csharp
// DEADLOCK — UI thread blocks, async work can't get back on UI thread
void OnButtonClick()
{
    var result = GetDataAsync().Result; // Deadlock!
}

// FIX — Use JoinableTaskFactory.Run
void OnButtonClick()
{
    var result = ThreadHelper.JoinableTaskFactory.Run(async () =>
    {
        return await GetDataAsync();
    });
}
```

### Anti-pattern: async void event handler

```csharp
// CRASH RISK — unhandled exceptions in async void terminate the process
async void OnWindowLoaded(object sender, EventArgs e)
{
    await InitializeAsync(); // If this throws, VS crashes
}

// FIX — wrap with JoinableTaskFactory.RunAsync
void OnWindowLoaded(object sender, EventArgs e)
{
    _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
    {
        await InitializeAsync();
    });
}
```

### Anti-pattern: Legacy thread switching

```csharp
// WRONG — VSTHRD001
ThreadHelper.Generic.Invoke(() => UpdateUI());
Dispatcher.CurrentDispatcher.BeginInvoke(() => UpdateUI());

// FIX
await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
UpdateUI();
```

### Anti-pattern: Missing ThrowIfNotOnUIThread

```csharp
// WRONG — calling IVs* method without thread check (VSTHRD010)
void UseVsService()
{
    IVsSolution sln = GetSolution();
    sln.SetProperty(/*...*/); // May fail or corrupt state if not on UI thread
}

// FIX
void UseVsService()
{
    ThreadHelper.ThrowIfNotOnUIThread();
    IVsSolution sln = GetSolution();
    sln.SetProperty(/*...*/);
}
```

---

## Quick reference

| Scenario | Pattern |
|----------|---------|
| Switch to UI thread (async) | `await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();` |
| Switch to background (async) | `await TaskScheduler.Default;` or `await Task.Run(...)` |
| Assert on UI thread (sync) | `ThreadHelper.ThrowIfNotOnUIThread();` |
| Call async from sync | `ThreadHelper.JoinableTaskFactory.Run(async () => await ...);` |
| Fire-and-forget | `_ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => ...);` |
| Event handler (async) | Wrap body with `JoinableTaskFactory.RunAsync`, avoid `async void` |
| Lazy async init | `new AsyncLazy<T>(factory, ThreadHelper.JoinableTaskFactory)` |
| Honor cancellation | Pass `CancellationToken` through; use `DisposalToken` in packages |
| Long blocking with dialog | `ThreadHelper.JoinableTaskFactory.Run("title", async (progress, ct) => ...)` |
| Deferred UI work at idle | `await ThreadHelper.JoinableTaskFactory.StartOnIdle(async delegate { ... });` |

## Troubleshooting

- **IDE freezes (hangs) when running extension code:** You're blocking the UI thread with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`. Replace with `await` or wrap in `JoinableTaskFactory.Run`. Run the VSTHRD analyzers to find all instances.
- **Deadlock on `SwitchToMainThreadAsync`:** The UI thread is blocked by a synchronous `.Result`/`.Wait()` call further up the call stack, preventing `SwitchToMainThreadAsync` from completing. Audit the call chain for any synchronous blocking. Use `JoinableTaskFactory.Run` as the outermost sync-over-async bridge.
- **`InvalidCastException` or `RPC_E_WRONG_THREAD` when calling IVs* services:** You're calling STA COM objects from a background thread. Add `await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` before the call, or add `ThreadHelper.ThrowIfNotOnUIThread()` to catch it early in debug.
- **VS crashes with no clear stack trace:** An `async void` method threw an unhandled exception. Search for `async void` in your codebase and convert to `async Task` with `JoinableTaskFactory.RunAsync` for fire-and-forget scenarios.
- **VSTHRD analyzers show warnings but extension seems to work:** The threading issues are intermittent — they may only deadlock under load or on slower machines. Fix all VSTHRD warnings; they represent real bugs that will eventually surface in production.
- **`ConfigureAwait(false)` causes intermittent failures:** In VS extension code, `ConfigureAwait(false)` bypasses the `JoinableTaskFactory` synchronization context. Remove it and use explicit thread switching (`await TaskScheduler.Default`) instead.

## What NOT to do

> **Do NOT** use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on tasks — causes deadlocks on the UI thread. Use `await` or `JoinableTaskFactory.Run`. (VSTHRD002 catches this.)

> **Do NOT** use `async void` methods or lambdas — unhandled exceptions crash the entire VS process. Return `Task` and use `JoinableTaskFactory.RunAsync` for fire-and-forget.

> **Do NOT** use `ConfigureAwait(false)` in VS extension code — bypasses `JoinableTaskFactory`'s `SynchronizationContext`, causing hard-to-debug threading issues.

> **Do NOT** use `Thread.Sleep()` — blocks the current thread completely. Use `await Task.Delay()` instead.

> **Do NOT** use `Task.Run()` to wrap calls to `IVs*` COM interfaces — most are STA and must be called from the UI thread. Calling from a thread-pool thread causes `RPC_E_WRONG_THREAD`.

> **Do NOT** use `Dispatcher.Invoke`/`BeginInvoke` or `ThreadHelper.Generic.Invoke` to switch threads — use `await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()`. (VSTHRD001 catches this.)

> **Do NOT** call `SwitchToMainThreadAsync()` without `await` — the call does nothing until awaited. (VSTHRD004 catches this.)

> **Do NOT** use `Lazy<T>` for async initialization — use `AsyncLazy<T>` with `JoinableTaskFactory`. `Lazy<T>` can deadlock because its lock doesn't yield. (VSTHRD011 catches this.)

## See also

- [vs-error-handling](../handling-extension-errors/SKILL.md)
- [vs-background-tasks-progress](../showing-background-progress/SKILL.md)
- [vs-commands](../adding-commands/SKILL.md)
- [vs-tool-window](../adding-tool-windows/SKILL.md)
- [vs-solution-events](../handling-solution-events/SKILL.md)

## References

- [Managing Multiple Threads in Managed Code](https://learn.microsoft.com/visualstudio/extensibility/managing-multiple-threads-in-managed-code)
- [Cookbook for Visual Studio Threading](https://github.com/microsoft/vs-threading/blob/main/docfx/docs/cookbook_vs.md)
- [Threading Analyzer Rules (VSTHRD)](https://github.com/microsoft/vs-threading/blob/main/docfx/analyzers/index.md)
- [Microsoft.VisualStudio.Threading.Analyzers NuGet](https://www.nuget.org/packages/Microsoft.VisualStudio.Threading.Analyzers)
- [JoinableTaskFactory API](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.threading.joinabletaskfactory)
- [VisualStudio.Extensibility Commands](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/command/command)

---
name: handling-extension-errors
description: Handle errors and exceptions in Visual Studio extensions. Use when the user asks about try/catch patterns, logging exceptions, Output Window logging, ActivityLog, TraceSource diagnostics, telemetry, or notifying users about errors in a Visual Studio IDE extension. Covers the VisualStudio.Extensibility (out-of-process) model, VSSDK Community Toolkit, and legacy VSSDK patterns.
---

# Error and Exception Handling in Visual Studio Extensions

Exceptions will occur in any extension. Handle them in a way that:
1. Captures enough detail for you to diagnose and fix the issue.
2. Informs the user at an appropriate level of severity.
3. Never crashes Visual Studio.

Unhandled exceptions in an in-process extension can crash the entire IDE, not just your feature. Even in the out-of-process model, unhandled exceptions terminate your extension host process, breaking all functionality until VS restarts it. Proper error handling also means choosing the right notification channel — a modal error dialog for a recoverable background issue trains users to ignore your extension, while silently swallowing errors makes bugs invisible. The patterns here ensure errors are always logged (for you) and surfaced at the right severity (for the user).

**When to use this vs. alternatives:**
- Logging + notifying the user about errors → **this skill**
- Avoiding threading mistakes that cause crashes → [vs-async-threading](../handling-async-threading/SKILL.md)
- Adding items to the VS Error List (build errors, warnings) → [vs-error-list](../integrating-error-list/SKILL.md)
- Showing a non-blocking info bar notification → [vs-info-bar](../showing-info-bars/SKILL.md)
- Showing a message box for high-severity errors → [vs-message-box](../showing-message-boxes/SKILL.md)

---

## Strategy: pick the right response by severity

| Severity | Action |
|----------|--------|
| **Low** — user flow unaffected | Log to Output Window or TraceSource. Optionally show a status bar message. |
| **Medium** — user might want to know | Log + show an info bar in the relevant tool/document window. |
| **High** — user flow is interrupted | Log + show a message box / user prompt so the user can acknowledge or retry. |

Always log the exception regardless of severity.

---

## 1. Include debug symbols

Before anything else, ensure your `.pdb` files ship with the VSIX. In project properties, set:

> **Include Debug Symbols in VSIX Container** = `True`

This gives you accurate stack traces in bug reports.

---

## 2. Logging exceptions

### a) VSSDK Community Toolkit — Output Window

The simplest approach. The `Log()` / `LogAsync()` extension methods on `Exception` write the full stack trace to a dedicated Output Window pane.

**NuGet package:** `Community.VisualStudio.Toolkit`
**Key namespace:** `Community.VisualStudio.Toolkit`

**Synchronous context:**

```csharp
try
{
    // Do work
}
catch (Exception ex)
{
    ex.Log();
}
```

**Asynchronous context:**

```csharp
try
{
    // Do work
}
catch (Exception ex)
{
    await ex.LogAsync();
}
```

### b) VisualStudio.Extensibility — TraceSource

Each extension part can inject a `TraceSource` instance provided by the extensibility framework. Use it for structured diagnostics:

**NuGet package:** `Microsoft.VisualStudio.Extensibility`
**Key namespace:** `Microsoft.VisualStudio.Extensibility`

```csharp
using System.Diagnostics;

public class MyCommand : Command
{
    private readonly TraceSource _logger;

    public MyCommand(VisualStudioExtensibility extensibility, TraceSource traceSource)
        : base(extensibility)
    {
        _logger = traceSource;
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
    {
        try
        {
            // Do work
        }
        catch (Exception ex)
        {
            _logger.TraceEvent(TraceEventType.Error, 0, ex.ToString());
        }
    }
}
```

Logs are written to `%TEMP%\VSLogs` in `.svclog` XML format and can be viewed with [Microsoft Service Trace Viewer](https://learn.microsoft.com/dotnet/framework/wcf/service-trace-viewer-tool-svctraceviewer-exe).

### c) Custom Output Window pane

For VSIX Community Toolkit extensions, you can create your own Output Window pane for logging:

```csharp
OutputWindowPane pane = await VS.Windows.CreateOutputWindowPaneAsync("My Extension");
await pane.WriteLineAsync($"Error: {ex.Message}");
await pane.WriteLineAsync(ex.StackTrace);
```

### d) Activity log (VSSDK)

**NuGet package:** `Microsoft.VisualStudio.SDK`
**Key namespaces:** `Microsoft.VisualStudio.Shell`, `Microsoft.VisualStudio.Shell.Interop`

```csharp
IVsActivityLog log = await GetServiceAsync(typeof(SVsActivityLog)) as IVsActivityLog;
log?.LogEntry(
    (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
    this.ToString(),
    $"An error occurred: {ex.Message}");
```

The activity log writes to `%APPDATA%\Microsoft\VisualStudio\<version>\ActivityLog.xml`.

---

## 3. Notifying the user about errors

### Low severity — Status bar

For Vsix Community Toolkit extensions, use:

```csharp
await VS.StatusBar.ShowMessageAsync("An error occurred. See the Output Window for details.");
```

**VSSDK:**

```csharp
IVsStatusbar statusBar = (IVsStatusbar)ServiceProvider.GetService(typeof(SVsStatusbar));
statusBar?.SetText("An error occurred. See the Output Window for details.");
```

### Medium severity — Info bar

**Community Toolkit:**

```csharp
var model = new InfoBarModel(
    new[] { new InfoBarTextSpan("Something went wrong. "),
            new InfoBarHyperlink("View details") });

InfoBar infoBar = await VS.InfoBar.CreateAsync(ToolWindowGuids80.SolutionExplorer, model);
await infoBar.TryShowInfoBarUIAsync();
```

> Info bars via raw VSSDK require `IVsInfoBarUIFactory` and `IVsInfoBarHost` — see the [vs-message-box skill](../showing-message-boxes/SKILL.md) for the full VSSDK info bar pattern.

### High severity — Message box

**Community Toolkit:**

```csharp
await VS.MessageBox.ShowAsync(
    "Error",
    $"An unexpected error occurred: {ex.Message}",
    OLEMSGICON.OLEMSGICON_CRITICAL,
    OLEMSGBUTTON.OLEMSGBUTTON_OK);
```

**VSSDK (`VsShellUtilities.ShowMessageBox`):**

```csharp
VsShellUtilities.ShowMessageBox(
    this.package,                          // IServiceProvider (your Package instance)
    $"An unexpected error occurred: {ex.Message}",  // message
    "Error",                               // title
    OLEMSGICON.OLEMSGICON_CRITICAL,        // icon
    OLEMSGBUTTON.OLEMSGBUTTON_OK,          // buttons
    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST); // default button
```

> `VsShellUtilities.ShowMessageBox` is the lowest-level VSSDK API and works without the Community Toolkit. The first parameter is any `IServiceProvider` — typically your `AsyncPackage` instance. It returns an `int` matching the `VSConstants.MessageBoxResult` values if you need to branch on the user's choice.

**VisualStudio.Extensibility:**

```csharp
await this.Extensibility.Shell().ShowPromptAsync(
    $"An unexpected error occurred: {ex.Message}",
    PromptOptions.ErrorConfirm,
    ct);
```

---

## 4. Complete error handling pattern

### VisualStudio.Extensibility (out-of-process)

```csharp
public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
{
    try
    {
        // Main logic
        await DoWorkAsync(ct);
    }
    catch (OperationCanceledException)
    {
        // User or system cancelled — no action needed
    }
    catch (Exception ex)
    {
        _logger.TraceEvent(TraceEventType.Error, 0, ex.ToString());

        await this.Extensibility.Shell().ShowPromptAsync(
            $"An error occurred: {ex.Message}",
            PromptOptions.ErrorConfirm,
            ct);
    }
}
```

### Community Toolkit (in-process)

```csharp
protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
{
    try
    {
        // Main logic
        await DoWorkAsync();
    }
    catch (OperationCanceledException)
    {
        // Cancelled — nothing to do
    }
    catch (Exception ex)
    {
        await ex.LogAsync();

        await VS.MessageBox.ShowAsync(
            "Error",
            $"An error occurred: {ex.Message}",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK);
    }
}
```

### VSSDK (in-process, legacy)

```csharp
private void Execute(object sender, EventArgs e)
{
    ThreadHelper.ThrowIfNotOnUIThread();
    try
    {
        // Main logic
        DoWork();
    }
    catch (OperationCanceledException)
    {
        // Cancelled — nothing to do
    }
    catch (Exception ex)
    {
        IVsActivityLog log = Package.GetGlobalService(typeof(SVsActivityLog)) as IVsActivityLog;
        log?.LogEntry(
            (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            nameof(MyCommand),
            ex.ToString());

        VsShellUtilities.ShowMessageBox(
            this.package,
            $"An error occurred: {ex.Message}",
            "Error",
            OLEMSGICON.OLEMSGICON_CRITICAL,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
```

---

## 5. Automated telemetry

For production extensions, integrate an APM system to catch errors you can't reproduce locally:

- **Application Insights** — `TelemetryClient.TrackException(ex)`
- Other options: Raygun, Google Analytics, Sentry

Always mention telemetry collection in your extension's privacy statement.

---

## 6. IGuardedOperations (editor extension points)

Visual Studio's editor infrastructure uses `IGuardedOperations` to catch exceptions from MEF extension points (classifiers, taggers, etc.) so a broken extension doesn't take down the editor. If you build editor extensions, your code already benefits from this.

You can also use it explicitly:

```csharp
[Import]
IGuardedOperations GuardedOperations { get; set; }

// Wraps the call and swallows + logs exceptions
GuardedOperations.CallExtensionPoint(this, () => {
    // Potentially failing work
});
```

---

## Key guidance

- **Always catch `OperationCanceledException` separately** — it's normal control flow, not an error.
- **Never swallow exceptions silently** — at minimum log them.
- **Ship .pdb files** to get meaningful stack traces.
- **Use `TraceSource`** in the new extensibility model; **use `ex.Log()`** in the Community Toolkit.
- **Match notification severity** to the user impact — don't show a modal dialog for a recoverable background error.

## Troubleshooting

- **Exceptions are silently swallowed — no log output:** You're catching exceptions without logging them. Every `catch` block should call `ex.Log()` (Toolkit), `_logger.TraceEvent(...)` (Extensibility), or write to the Activity Log (VSSDK) before any user notification.
- **Output Window pane doesn't appear:** The `ex.Log()` Toolkit helper writes to a pane named after your extension. Open the Output Window and check the **Show output from** dropdown — your pane may exist but not be selected.
- **TraceSource logs are empty (Extensibility):** Ensure you're injecting `TraceSource` via the constructor, not creating a new instance manually. The framework-provided `TraceSource` is wired to the VS logging infrastructure; a manually created one writes nowhere.
- **Activity log file is empty or missing:** The Activity Log is only written when VS is launched with `/Log`. For development use `%APPDATA%\Microsoft\VisualStudio\<version>\ActivityLog.xml`. Consider the Output Window or TraceSource for day-to-day logging instead.
- **VS crashes with no exception in your logs:** An `async void` method threw an unhandled exception. Search for `async void` and convert to `async Task`. See [vs-async-threading](../handling-async-threading/SKILL.md).
- **User sees raw stack traces in error dialogs:** You're passing `ex.ToString()` or `ex.StackTrace` to the message box. Log the full exception, but show only `ex.Message` (or a friendlier summary) to the user.

## What NOT to do

> **Do NOT** swallow exceptions silently with empty `catch` blocks — at minimum, log to the Output Window, TraceSource, or Activity Log.

> **Do NOT** show raw exception messages or stack traces in user-facing message boxes — they may contain file paths or internal type names. Log the full exception; show a user-friendly summary.

> **Do NOT** use `async void` — unhandled exceptions crash the VS process. Return `Task` and use `JoinableTaskFactory.RunAsync` for fire-and-forget.

> **Do NOT** show modal error dialogs for recoverable background errors or during solution load — use an info bar or status bar message instead.

> **Do NOT** create a new `TraceSource` manually in VisualStudio.Extensibility extensions — inject the framework-provided one through the constructor.

## See also

- [vs-async-threading](../handling-async-threading/SKILL.md)
- [vs-message-box](../showing-message-boxes/SKILL.md)
- [vs-info-bar](../showing-info-bars/SKILL.md)
- [vs-error-list](../integrating-error-list/SKILL.md)

## References

- [Error handling recipe (VSSDK Community Toolkit)](https://learn.microsoft.com/visualstudio/extensibility/vsix/recipes/handle-errors)
- [Logging extension diagnostics (VisualStudio.Extensibility)](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/inside-the-sdk/logging)
- [Notifications recipe (VSSDK Community Toolkit)](https://learn.microsoft.com/visualstudio/extensibility/vsix/recipes/notifications)
- [IGuardedOperations](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.utilities.iguardedoperations)

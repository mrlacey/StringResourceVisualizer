---
name: adding-options-settings
description: Add custom settings and options pages to Visual Studio extensions. Use when the user asks how to create a Tools > Options page, store extension settings, read or write user preferences, use BaseOptionModel, DialogPage, or the VisualStudio.Extensibility settings API in a Visual Studio IDE extension. Covers VisualStudio.Extensibility (out-of-process), VSIX Community Toolkit (in-process), and legacy VSSDK (in-process) approaches.
---

# Adding Settings and Options Pages to Visual Studio Extensions

Most extensions need user-configurable settings exposed in the **Tools > Options** dialog. Visual Studio provides three different APIs depending on your extensibility model.

Without a proper settings page, extensions either hard-code behavior (frustrating users who need different defaults) or invent custom storage that doesn't integrate with VS's settings import/export, roaming, or reset. The Tools > Options page is the standard location users check for extension settings, and VS handles persistence, threading, and UI generation for you.

**When to use this vs. alternatives:**
- User-configurable settings with auto-generated Tools > Options page → **Options/Settings** (this skill)
- Custom font and color items with per-theme defaults → [vs-fonts-and-colors](../registering-fonts-colors/SKILL.md)
- Key bindings and command customization → [vs-commands](../adding-commands/SKILL.md)
- Workspace-level settings for Open Folder mode → [vs-open-folder](../extending-open-folder/SKILL.md) (`.vs/VSWorkspaceSettings.json`)

## Decision guide

| Approach | Settings storage | UI | Thread-safe | Lazy load |
|----------|-----------------|-----|-------------|-----------|
| **VisualStudio.Extensibility** | VS settings store | Auto-generated from setting definitions | Yes | Yes |
| **Community Toolkit** (`BaseOptionModel<T>`) | VS settings store / registry | Auto-generated property grid | Yes | Yes — package doesn't need to load |
| **VSSDK** (`DialogPage`) | Registry | Property grid (or custom WPF via `Window` override) | Manual | Only if `AllowsBackgroundLoading = true` |

---

## 1. VisualStudio.Extensibility (out-of-process, recommended)

Settings are declared as static properties with the `[VisualStudioContribution]` attribute. VS automatically generates a UI in the **Tools > Options** dialog and handles persistence.

**NuGet package:** `Microsoft.VisualStudio.Extensibility`
**Key namespace:** `Microsoft.VisualStudio.Extensibility`

> **Note:** As of VS 2022 17.x, the settings API is in preview. Add `#pragma warning disable VSEXTPREVIEW_SETTINGS` to suppress the experimental warning.

### Step 1: Define settings and categories

Settings are organized into categories. Place definitions in any class in your extension:

```csharp
#pragma warning disable VSEXTPREVIEW_SETTINGS

internal static class SettingDefinitions
{
    [VisualStudioContribution]
    internal static SettingCategory General { get; } = new("general", "General")
    {
        Description = "General extension settings",
        GenerateObserverClass = true,
    };

    [VisualStudioContribution]
    internal static Setting.Boolean EnableFeature { get; } = new(
        "enableFeature", "Enable Feature", General, defaultValue: true)
    {
        Description = "Enables the main feature of this extension",
    };

    [VisualStudioContribution]
    internal static Setting.String OutputPrefix { get; } = new(
        "outputPrefix", "Output Prefix", General, defaultValue: "[MyExt]")
    {
        Description = "Prefix added to output messages",
        MaxStringLength = 50,
    };

    [VisualStudioContribution]
    internal static Setting.Integer MaxResults { get; } = new(
        "maxResults", "Max Results", General, defaultValue: 100)
    {
        Description = "Maximum number of results to display",
        Minimum = 1,
        Maximum = 1000,
    };

    [VisualStudioContribution]
    internal static Setting.Enum Theme { get; } = new(
        "theme", "Theme", General, defaultValue: "dark",
        new("dark", "Dark"),
        new("light", "Light"),
        new("system", "System Default"));
}
```

### Setting types

| Type | Use case |
|------|----------|
| `Setting.Boolean` | True/false toggle |
| `Setting.String` | Free-text input |
| `Setting.Integer` | Whole number |
| `Setting.Decimal` | Floating point |
| `Setting.Enum` | Dropdown from predefined choices |
| `Setting.FormattedString` | Dates, times, IP addresses, emails, URIs, file/directory paths |
| `Setting.StringArray` | List of strings |
| `Setting.EnumArray` | Multi-select from predefined choices |
| `Setting.ObjectArray` | Sequence of items with multiple typed properties |

### Step 2: Nested categories

```csharp
[VisualStudioContribution]
internal static SettingCategory Advanced { get; } = new("advanced", "Advanced", General);

[VisualStudioContribution]
internal static Setting.Boolean DebugMode { get; } = new(
    "debugMode", "Debug Mode", Advanced, defaultValue: false);
```

### Step 3: Register the settings observer for dependency injection

In your `Extension` class, register the observer so it can be injected:

```csharp
[VisualStudioContribution]
internal class MyExtension : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new("MyExtension", ExtensionAssemblyVersion, "Publisher",
            "Description of my extension"),
    };

    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSettingsObservers();
        base.InitializeServices(serviceCollection);
    }
}
```

### Step 4: Read settings from a command

**One-shot read (no observer needed):**

```csharp
public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
{
    var result = await Extensibility.Settings().ReadEffectiveValueAsync(
        SettingDefinitions.EnableFeature, ct);
    bool isEnabled = result.ValueOrDefault(defaultValue: true);

    // Read multiple settings at once
    var results = await Extensibility.Settings().ReadEffectiveValuesAsync(
        [SettingDefinitions.EnableFeature, SettingDefinitions.MaxResults], ct);
    int maxResults = results.ValueOrDefault(SettingDefinitions.MaxResults, defaultValue: 100);
}
```

**Using the generated observer (for continuous monitoring):**

```csharp
public class MyCommand : Command
{
    private readonly Settings.GeneralObserver _settingsObserver;

    public MyCommand(VisualStudioExtensibility extensibility, Settings.GeneralObserver settingsObserver)
        : base(extensibility)
    {
        _settingsObserver = settingsObserver;
    }

    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken ct)
    {
        var snapshot = await _settingsObserver.GetSnapshotAsync(ct);
        bool isEnabled = snapshot.EnableFeature.ValueOrDefault(defaultValue: true);
        string prefix = snapshot.OutputPrefix.ValueOrDefault(defaultValue: "[MyExt]");
    }
}
```

### Step 5: React to setting changes

```csharp
public MyToolWindow(Settings.GeneralObserver settingsObserver)
{
    // The Changed handler fires at least once with the current values
    settingsObserver.Changed += OnSettingsChanged;
}

private async Task OnSettingsChanged(Settings.GeneralSnapshot snapshot)
{
    bool isEnabled = snapshot.EnableFeature.ValueOrDefault(defaultValue: true);
    // Update UI or behavior
}
```

### Step 6: Write settings programmatically

```csharp
var writeResult = await Extensibility.Settings().WriteAsync(
    batch =>
    {
        batch.WriteSetting(SettingDefinitions.EnableFeature, value: false);
        batch.WriteSetting(SettingDefinitions.OutputPrefix, value: "[Updated]");
    },
    description: "Reset settings to defaults",
    cancellationToken);
```

### Step 7: Monitor a single setting without an observer

```csharp
IDisposable subscription = await Extensibility.Settings().SubscribeAsync(
    SettingDefinitions.EnableFeature,
    cancellationToken,
    changeHandler: result =>
    {
        bool isEnabled = result.ValueOrDefault(defaultValue: true);
        // React to the change
    });

// Dispose to stop monitoring
subscription.Dispose();
```

### Localization

Use string resource references for display names and descriptions:

```csharp
[VisualStudioContribution]
internal static SettingCategory General { get; } = new("general", "%MyExtension.Settings.General%")
{
    Description = "%MyExtension.Settings.General.Description%",
    GenerateObserverClass = true,
};
```

---

## 2. VSIX Community Toolkit (in-process)

The Community Toolkit provides `BaseOptionModel<T>` — a thread-safe, lazy-loading options model. Settings are simple C# properties. The Toolkit handles persistence to the VS settings store and exposes the settings in the **Tools > Options** dialog via a property grid.

**NuGet package:** `Community.VisualStudio.Toolkit`
**Key namespace:** `Community.VisualStudio.Toolkit`

### Step 1: Create the options model

Create an `Options/` folder and add your settings class:

**Options/General.cs:**

```csharp
using System.ComponentModel;
using Community.VisualStudio.Toolkit;

internal partial class OptionsProvider
{
    // Register the options with these attributes on your package class:
    // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
    // [ProvideProfile(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
    public class GeneralOptions : BaseOptionPage<General> { }
}

public class General : BaseOptionModel<General>
{
    [Category("Features")]
    [DisplayName("Enable Feature")]
    [Description("Enables the main feature of this extension.")]
    [DefaultValue(true)]
    public bool EnableFeature { get; set; } = true;

    [Category("Features")]
    [DisplayName("Max Results")]
    [Description("Maximum number of results to display.")]
    [DefaultValue(100)]
    public int MaxResults { get; set; } = 100;

    [Category("Output")]
    [DisplayName("Output Prefix")]
    [Description("Prefix added to output messages.")]
    [DefaultValue("[MyExt]")]
    public string OutputPrefix { get; set; } = "[MyExt]";

    [Category("Appearance")]
    [DisplayName("Theme")]
    [Description("Select the color theme for the extension UI.")]
    [DefaultValue(ThemeOption.Dark)]
    [TypeConverter(typeof(EnumConverter))]
    public ThemeOption Theme { get; set; } = ThemeOption.Dark;
}

public enum ThemeOption
{
    Dark,
    Light,
    System,
}
```

### Step 2: Register in the package

```csharp
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
[ProvideProfile(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
public sealed class MyExtensionPackage : ToolkitPackage
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await this.RegisterCommandsAsync();
    }
}
```

- **`ProvideOptionPage`** — makes the page visible in the **Tools > Options** dialog.
- **`ProvideProfile`** — enables roaming and import/export. Optional but recommended.

### Step 3: Read and write settings

**Synchronous (e.g., from `BeforeQueryStatus`):**

```csharp
bool isEnabled = General.Instance.EnableFeature;
int max = General.Instance.MaxResults;

// Write
General.Instance.MaxResults = 200;
General.Instance.Save();
```

**Asynchronous (recommended):**

```csharp
protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
{
    General settings = await General.GetLiveInstanceAsync();
    bool isEnabled = settings.EnableFeature;
    string prefix = settings.OutputPrefix;

    // Write
    settings.MaxResults = 200;
    await settings.SaveAsync();
}
```

### Step 4: React to setting changes

```csharp
General.Saved += OnSettingsSaved;

private void OnSettingsSaved(object sender, General e)
{
    bool isEnabled = e.EnableFeature;
    // Update behavior based on new settings
}
```

### Multiple option pages

Create additional option classes for separate pages:

**Options/Advanced.cs:**

```csharp
internal partial class OptionsProvider
{
    public class AdvancedOptions : BaseOptionPage<Advanced> { }
}

public class Advanced : BaseOptionModel<Advanced>
{
    [Category("Diagnostics")]
    [DisplayName("Debug Mode")]
    [Description("Enables verbose logging.")]
    [DefaultValue(false)]
    public bool DebugMode { get; set; } = false;
}
```

Register the additional page:

```csharp
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
[ProvideProfile(typeof(OptionsProvider.GeneralOptions), "MyExtension", "General", 0, 0, true)]
[ProvideOptionPage(typeof(OptionsProvider.AdvancedOptions), "MyExtension", "Advanced", 0, 0, true)]
[ProvideProfile(typeof(OptionsProvider.AdvancedOptions), "MyExtension", "Advanced", 0, 0, true)]
public sealed class MyExtensionPackage : ToolkitPackage { ... }
```

---

## 3. VSSDK (in-process, legacy)

The low-level VSSDK uses `DialogPage` for the property-grid UI, with persistence handled automatically via the VS registry.

**NuGet package:** `Microsoft.VisualStudio.SDK`
**Key namespaces:** `Microsoft.VisualStudio.Shell`, `System.ComponentModel`

### Step 1: Create the DialogPage

```csharp
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

public class GeneralOptionsPage : DialogPage
{
    [Category("Features")]
    [DisplayName("Enable Feature")]
    [Description("Enables the main feature of this extension.")]
    public bool EnableFeature { get; set; } = true;

    [Category("Features")]
    [DisplayName("Max Results")]
    [Description("Maximum number of results to display.")]
    public int MaxResults { get; set; } = 100;

    [Category("Output")]
    [DisplayName("Output Prefix")]
    [Description("Prefix added to output messages.")]
    public string OutputPrefix { get; set; } = "[MyExt]";
}
```

### Step 2: Register with the package

```csharp
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideOptionPage(typeof(GeneralOptionsPage), "MyExtension", "General", 0, 0, true)]
[Guid("YOUR-PACKAGE-GUID")]
public sealed class MyExtensionPackage : AsyncPackage
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        // Package initialization
    }
}
```

### Step 3: Read settings from the package

```csharp
public GeneralOptionsPage GetOptionsPage()
{
    return (GeneralOptionsPage)GetDialogPage(typeof(GeneralOptionsPage));
}
```

From a command handler:

```csharp
private void Execute(object sender, EventArgs e)
{
    ThreadHelper.ThrowIfNotOnUIThread();

    var package = (MyExtensionPackage)this._package;
    GeneralOptionsPage options = package.GetOptionsPage();

    bool isEnabled = options.EnableFeature;
    int maxResults = options.MaxResults;
}
```

### Custom WPF UI (advanced)

Override the `Window` property to provide a custom UI instead of the default property grid:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;

[Guid("00000000-0000-0000-0000-000000000000")]
public class AdvancedOptionsPage : UIElementDialogPage
{
    private AdvancedOptionsControl _control;

    protected override UIElement Child => _control ??= new AdvancedOptionsControl(this);

    public bool DebugMode { get; set; } = false;

    public override void SaveSettingsToStorage()
    {
        _control?.SaveToPage();
        base.SaveSettingsToStorage();
    }

    public override void LoadSettingsFromStorage()
    {
        base.LoadSettingsFromStorage();
        _control?.LoadFromPage();
    }
}
```

### Reading/Writing the settings store directly (VSSDK)

For settings that don't need a UI page:

```csharp
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;

var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
WritableSettingsStore userSettings = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

// Write
if (!userSettings.CollectionExists("MyExtension"))
    userSettings.CreateCollection("MyExtension");
userSettings.SetBoolean("MyExtension", "EnableFeature", true);
userSettings.SetInt32("MyExtension", "MaxResults", 100);

// Read
bool isEnabled = userSettings.GetBoolean("MyExtension", "EnableFeature", defaultValue: true);
int maxResults = userSettings.GetInt32("MyExtension", "MaxResults", defaultValue: 100);
```

---

## Key guidance

- **New extensions** → use `VisualStudio.Extensibility` settings with `GenerateObserverClass = true` for reactive, type-safe settings.
- **Existing Toolkit extensions** → use `BaseOptionModel<T>`. Simple, thread-safe, lazy-loading.
- **Legacy VSSDK** → use `DialogPage` with `ProvideOptionPage`. Consider `UIElementDialogPage` for custom WPF UI.
- Always provide `[DefaultValue]` attributes (Toolkit/VSSDK) or `defaultValue` parameters (Extensibility) so settings have sensible defaults before the user changes them.
- Never access settings on the UI thread synchronously when an async alternative exists.
- Use `ProvideProfile` (Toolkit/VSSDK) to enable roaming and import/export.

## Troubleshooting

- **Options page doesn't appear in Tools > Options:** Verify `[ProvideOptionPage]` is on the package class (Toolkit/VSSDK) with the correct `typeof(OptionsProvider.XxxOptions)`. For Extensibility, ensure the `[VisualStudioContribution]` attribute is present on your settings class.
- **Settings reset on every VS restart:** You're not calling `Save()` / `SaveAsync()` after changing values. For VSSDK `DialogPage`, persistence is automatic but only if you don't override `SaveSettingsToStorage` incorrectly.
- **`BaseOptionModel<T>` properties don't persist:** Ensure properties have `public` getters and setters. Private setters won't be serialized.
- **Custom WPF options page shows blank:** You're using `DialogPage` instead of `UIElementDialogPage`. Override the `Child` property (not `Window`) and return your WPF `UserControl`.
- **Settings changes don't take effect until VS restart:** Subscribe to `General.Saved` (Toolkit) or implement `IProfileManager` (VSSDK) to react immediately when the user clicks OK.

## What NOT to do

> **Do NOT** store settings in custom files (JSON, XML) — use the VS settings store, which handles roaming, import/export, and reset automatically.

> **Do NOT** access `DialogPage` properties from a background thread without `SwitchToMainThreadAsync()` — they're backed by the VS registry which may require the UI thread.

> **Do NOT** use `Registry.CurrentUser` directly — use `WritableSettingsStore` (VSSDK) or `BaseOptionModel<T>` (Toolkit) to write to the correct VS hive.

> **Do NOT** forget `[DefaultValue]` attributes — without them, the "Reset" button in Tools > Options won't restore sensible defaults.

## See also

- [vs-fonts-and-colors](../registering-fonts-colors/SKILL.md)
- [vs-open-folder](../extending-open-folder/SKILL.md)
- [vs-commands](../adding-commands/SKILL.md)

## References

- [VisualStudio.Extensibility settings](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/settings/settings)
- [Custom settings and options (Community Toolkit)](https://learn.microsoft.com/visualstudio/extensibility/vsix/recipes/settings-options)
- [Create an options page (VSSDK)](https://learn.microsoft.com/visualstudio/extensibility/creating-an-options-page)
- [Options and Options Pages](https://learn.microsoft.com/visualstudio/extensibility/internals/options-and-options-pages)

---
name: listening-text-view-events
description: React to text editor lifecycle events such as open, close, and content changes. Use when the user asks how to detect when an editor opens, listen for file open events, track active editor changes, respond to text changes in the editor, implement IWpfTextViewCreationListener, or use ITextViewOpenClosedListener. Covers VisualStudio.Extensibility (out-of-process), VSIX Community Toolkit (in-process), and legacy VSSDK (in-process) approaches.
---

# Text View Listeners in Visual Studio Extensions

Text view listeners let you react when editors are opened, closed, or when their content changes. This is a common entry point for extensions that need to initialize per-editor state. Common scenarios:

- Initialize extension features when a specific file type is opened
- Track which documents are currently open
- React to text changes for live analysis or decoration
- Clean up resources when an editor is closed

Text view listeners are the foundational hook that most editor extensions use as their entry point. Adornments, taggers, classifiers, and margins all need to know when an editor opens — but text view listeners are also useful on their own for per-document state initialization or for extensions that need to react to text changes without producing visual output. The VisualStudio.Extensibility model provides `ITextViewOpenClosedListener` as a simpler, out-of-process alternative.

**When to use text view listeners vs. alternatives:**
- React to editor open/close for initialization → **text view listener** (this skill)
- Add visual decorations when an editor opens → combine with [vs-editor-adornment](../adding-editor-adornments/SKILL.md)
- React to text changes for tagging → use a tagger's `ITextBuffer.Changed` subscription (see [vs-editor-tagger](../creating-editor-taggers/SKILL.md))
- React to document save/close events → [vs-file-document-ops](../managing-files-documents/SKILL.md)

---

## MEF Asset Type Requirement

**Any extension that uses MEF editor exports must declare the MEF asset type in the `.vsixmanifest` file.** Without this, Visual Studio will not discover your MEF components and your listener will not load.

Add this inside the `<Assets>` element of `source.extension.vsixmanifest`:

```xml
<Asset Type="Microsoft.VisualStudio.MefComponent"
       d:Source="Project"
       d:ProjectName="%CurrentProject%"
       Path="|%CurrentProject%|" />
```

> **Note:** The VisualStudio.Extensibility approach does NOT require this MEF asset entry — it uses its own discovery mechanism.

---

## 1. VisualStudio.Extensibility (out-of-process, recommended)

The new model provides `ITextViewOpenClosedListener` and `ITextViewChangedListener` for reacting to editor events **out-of-process**.

**NuGet package:** `Microsoft.VisualStudio.Extensibility`
**Key namespace:** `Microsoft.VisualStudio.Extensibility.Editor`

### Listen for editor open/close

```csharp
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace MyExtension;

[VisualStudioContribution]
internal sealed class MyTextViewListener
    : ExtensionPart, ITextViewOpenClosedListener
{
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = new[]
        {
            DocumentFilter.FromDocumentType("CSharp"),
        },
    };

    public Task TextViewOpenedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        // Called when a matching text view is opened
        System.Diagnostics.Debug.WriteLine($"Opened: {textView.Document.Uri}");
        return Task.CompletedTask;
    }

    public Task TextViewClosedAsync(ITextViewSnapshot textView, CancellationToken cancellationToken)
    {
        // Called when the text view is closed
        System.Diagnostics.Debug.WriteLine($"Closed: {textView.Document.Uri}");
        return Task.CompletedTask;
    }
}
```

### Listen for text changes

```csharp
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace MyExtension;

[VisualStudioContribution]
internal sealed class MyTextChangeListener
    : ExtensionPart, ITextViewChangedListener
{
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = new[]
        {
            DocumentFilter.FromGlobPattern("**/*.cs"),
        },
    };

    public Task TextViewChangedAsync(
        TextViewChangedArgs args,
        CancellationToken cancellationToken)
    {
        // React to text changes
        foreach (var change in args.AfterTextView.Document.AsTextDocument().Changes)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Changed at position {change.Position}: '{change.OldText}' -> '{change.NewText}'");
        }

        return Task.CompletedTask;
    }
}
```

### Document filters

Use `DocumentFilter` to scope which files trigger your listener:

```csharp
// By document/content type
DocumentFilter.FromDocumentType("CSharp")
DocumentFilter.FromDocumentType("code")

// By file glob pattern
DocumentFilter.FromGlobPattern("**/*.cs")
DocumentFilter.FromGlobPattern("**/appsettings*.json")
```

---

## 2. VSIX Community Toolkit (in-process)

The Community Toolkit does not wrap text view creation listeners — use the standard VSSDK MEF pattern below.

---

## 3. VSSDK (in-process, legacy)

**NuGet packages:** `Microsoft.VisualStudio.SDK`, `Microsoft.VisualStudio.Editor`, `Microsoft.VisualStudio.Text.UI.Wpf`
**Key namespaces:** `Microsoft.VisualStudio.Text.Editor`, `Microsoft.VisualStudio.Utilities`

### Listen for editor open with `IWpfTextViewCreationListener`

```csharp
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MyExtension;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("CSharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class MyTextViewCreationListener : IWpfTextViewCreationListener
{
    public void TextViewCreated(IWpfTextView textView)
    {
        // Called when a C# document editor is created

        // Subscribe to text changes
        textView.TextBuffer.Changed += OnTextBufferChanged;

        // Subscribe to editor close
        textView.Closed += OnTextViewClosed;
    }

    private void OnTextBufferChanged(object sender, Microsoft.VisualStudio.Text.TextContentChangedEventArgs e)
    {
        foreach (var change in e.Changes)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Changed: '{change.OldText}' -> '{change.NewText}'");
        }
    }

    private void OnTextViewClosed(object sender, System.EventArgs e)
    {
        if (sender is IWpfTextView textView)
        {
            // Clean up event handlers to avoid leaks
            textView.TextBuffer.Changed -= OnTextBufferChanged;
            textView.Closed -= OnTextViewClosed;
        }
    }
}
```

### Listen for editor open with `ITextViewCreationListener` (non-WPF)

For scenarios that don't need WPF access:

```csharp
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MyExtension;

[Export(typeof(ITextViewCreationListener))]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class MyTextViewListener : ITextViewCreationListener
{
    public void TextViewCreated(ITextView textView)
    {
        // Works for any text view, not just WPF views
    }
}
```

### Common text view roles

Use `[TextViewRole]` to filter which editor instances trigger your listener:

| Role | Description |
|------|-------------|
| `PredefinedTextViewRoles.Document` | Main document editors |
| `PredefinedTextViewRoles.Interactive` | Interactive windows (e.g., C# Interactive) |
| `PredefinedTextViewRoles.Editable` | Any editable view |
| `PredefinedTextViewRoles.PrimaryDocument` | Primary (not secondary/split) document view |
| `PredefinedTextViewRoles.EmbeddedPeekTextView` | Peek Definition views |

### Getting the file path from a text view

```csharp
using Microsoft.VisualStudio.Text;

// Inside TextViewCreated:
if (textView.TextBuffer.Properties.TryGetProperty(
    typeof(ITextDocument), out ITextDocument document))
{
    string filePath = document.FilePath;
}
```

### Key points

- Always unsubscribe from events in the `Closed` handler to prevent memory leaks.
- Use `[ContentType]` to limit which file types trigger your listener.
- Use `[TextViewRole]` to avoid triggering in peek windows, diff views, etc.
- `TextViewCreated` runs on the UI thread — keep it fast.
- Use `textView.TextBuffer.Properties` to store per-view state.
- For WPF-specific operations (adornments, margins), use `IWpfTextViewCreationListener`.
- **Remember to add the MEF asset type to your `.vsixmanifest`** for the VSSDK approach.

---

## Key guidance

- **VisualStudio.Extensibility** — Use `ITextViewOpenClosedListener` and `ITextViewChangedListener` with `DocumentFilter` for scoping. No MEF asset required.
- **VSSDK / Community Toolkit** — Export `IWpfTextViewCreationListener` or `ITextViewCreationListener` via MEF. Subscribe to `TextBuffer.Changed` and `Closed` events. Unsubscribe on close.
- Always declare the **MEF component asset type** in `source.extension.vsixmanifest` for the VSSDK approach.
- Prefer `PredefinedTextViewRoles.Document` to avoid triggering in non-document editors.

## Troubleshooting

- **Listener never fires:** Check the MEF asset type in `.vsixmanifest`. Verify `[ContentType]` and `[TextViewRole]` match. For Extensibility, ensure the `[VisualStudioContribution]` attribute is present.
- **Listener fires for peek, diff, and embedded editors:** You're missing `[TextViewRole(PredefinedTextViewRoles.Document)]`. Without it, the listener fires for all editor instances.
- **Memory leak when opening/closing files:** You're not unsubscribing from text view events in `ITextView.Closed`. Always subscribe to `Closed` early and clean up there.
- **Text changes not detected:** Subscribe to `ITextBuffer.Changed` (VSSDK/Toolkit) or implement `ITextViewChangedListener` (Extensibility) — text view creation alone doesn't track ongoing edits.

## What NOT to do

> **Do NOT** do heavy work in the text view creation callback — it runs on the UI thread and blocks the editor from appearing.

> **Do NOT** forget the `[TextViewRole]` attribute — without it, your listener triggers for every text view instance (peek, diff, embedded), which may not be intended.

> **Do NOT** forget to unsubscribe from events when the text view closes — leaked subscriptions cause memory leaks and crashes.

> **Do NOT** forget the `MefComponent` asset type in `.vsixmanifest` for in-process extensions.

## See also

- [vs-editor-adornment](../adding-editor-adornments/SKILL.md)
- [vs-editor-tagger](../creating-editor-taggers/SKILL.md)
- [vs-editor-margin](../adding-editor-margins/SKILL.md)
- [vs-file-document-ops](../managing-files-documents/SKILL.md)

## References

- [ITextViewOpenClosedListener (VisualStudio.Extensibility)](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/editor/editor-concepts)
- [IWpfTextViewCreationListener (VSSDK)](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.text.editor.iwpftextviewcreationlistener)
- [Text View Roles](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.text.editor.predefinedtextviewroles)

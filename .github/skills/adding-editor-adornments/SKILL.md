---
name: adding-editor-adornments
description: Add visual adornments (decorations, overlays, highlights) to the Visual Studio text editor. Use when the user asks how to draw on the editor, add inline decorations, highlight text visually, add background colors to lines, show icons in the editor, create viewport-relative overlays, or render custom WPF visuals on top of editor text. Covers VisualStudio.Extensibility (out-of-process), VSIX Community Toolkit (in-process), and legacy VSSDK (in-process) approaches.
---

# Editor Adornments in Visual Studio Extensions

Adornments are WPF visuals drawn on the text editor surface. They can highlight text, add inline icons, render overlays, or display viewport-relative UI. Common scenarios:

- Highlight specific lines or text spans with a colored background
- Draw inline color swatches next to color literals
- Show an image or icon next to a code element
- Add a viewport-relative watermark or status overlay

Adornments are one of the most powerful editor integration points — they let you draw arbitrary WPF content directly on the code surface without modifying the underlying text. The critical constraint is performance: `LayoutChanged` fires on every scroll and edit, so adornment rendering must be fast.

**When to use adornments vs. alternatives:**
- Visual highlights, overlays, or decorations on the editor surface → **adornments** (this skill)
- Syntax coloring (keyword highlighting, language coloring) → classifier (see [vs-editor-classifier](../adding-editor-classifiers/SKILL.md))
- Gutter icons (per-line glyphs) → margin with `IGlyphFactory` (see [vs-editor-margin](../adding-editor-margins/SKILL.md))
- Hover tooltips on text → Quick Info (see [vs-editor-quickinfo](../adding-quickinfo-tooltips/SKILL.md))
- Inline metadata above code elements → CodeLens (see [vs-codelens](../adding-codelens-indicators/SKILL.md))

## Implementation checklist

- [ ] Add the MEF asset type to `.vsixmanifest`
- [ ] Define the adornment layer (`[Export(typeof(AdornmentLayerDefinition))]`)
- [ ] Create the adornment class that adds WPF visuals to the layer
- [ ] Create the text view creation listener to instantiate the adornment on editor open

---

## MEF Asset Type Requirement

**Any extension that uses MEF editor exports must declare the MEF asset type in the `.vsixmanifest` file.** Without this, Visual Studio will not discover your MEF components and your adornment will not load.

Add this inside the `<Assets>` element of `source.extension.vsixmanifest`:

```xml
<Asset Type="Microsoft.VisualStudio.MefComponent"
       d:Source="Project"
       d:ProjectName="%CurrentProject%"
       Path="|%CurrentProject%|" />
```

If you are using a multi-project solution, replace `%CurrentProject%` with the project name that contains the MEF exports. The Visual Studio VSIX project template usually includes this automatically, but if your adornment is not loading, this is the first thing to check.

---

## 1. VisualStudio.Extensibility (out-of-process, recommended)

The VisualStudio.Extensibility SDK does **not** currently support editor adornments. There is no out-of-process API for rendering custom WPF visuals on the editor surface.

**If you need editor adornments, use the VSIX Community Toolkit or VSSDK (in-process) approach.**

For some visual scenarios, you may be able to use `ITextViewChangedListener` combined with diagnostics/text markers in the new model, but full custom WPF adornments require in-process MEF.

---

## 2. VSIX Community Toolkit (in-process)

The Community Toolkit does not add a separate adornment API — adornments use the same MEF-based VSSDK APIs. The toolkit simplifies package initialization, but the adornment itself uses `IWpfTextViewCreationListener` and `AdornmentLayer` from the editor MEF model.

**NuGet packages:** `Community.VisualStudio.Toolkit`, `Microsoft.VisualStudio.Editor`, `Microsoft.VisualStudio.Text.UI.Wpf`
**Key namespaces:** `Microsoft.VisualStudio.Text.Editor`, `Microsoft.VisualStudio.Utilities`

The code below is identical to the VSSDK approach — the toolkit does not wrap this API.

---

## 3. VSSDK (in-process, legacy)

Editor adornments are created by:

1. Defining an adornment layer with `[Export(typeof(AdornmentLayerDefinition))]`
2. Implementing `IWpfTextViewCreationListener` to attach your adornment logic when an editor opens
3. Drawing WPF elements on the adornment layer in response to layout changes

**NuGet packages:** `Microsoft.VisualStudio.SDK`, `Microsoft.VisualStudio.Editor`, `Microsoft.VisualStudio.Text.UI.Wpf`
**Key namespaces:** `Microsoft.VisualStudio.Text.Editor`, `Microsoft.VisualStudio.Text.Formatting`, `Microsoft.VisualStudio.Utilities`

### Step 1: Define the adornment layer and listener

```csharp
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MyExtension;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class HighlightAdornmentFactory : IWpfTextViewCreationListener
{
    [Export(typeof(AdornmentLayerDefinition))]
    [Name("MyHighlightAdornment")]
    [Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
    private AdornmentLayerDefinition _editorAdornmentLayer;

    public void TextViewCreated(IWpfTextView textView)
    {
        // Create the adornment manager and attach it to the view
        new HighlightAdornment(textView);
    }
}
```

### Step 2: Implement the adornment logic

This example highlights every occurrence of "TODO" with a colored background:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace MyExtension;

internal sealed class HighlightAdornment
{
    private const string AdornmentLayerName = "MyHighlightAdornment";
    private readonly IAdornmentLayer _layer;
    private readonly IWpfTextView _view;
    private readonly Brush _brush;

    public HighlightAdornment(IWpfTextView view)
    {
        _view = view;
        _layer = view.GetAdornmentLayer(AdornmentLayerName);

        _brush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0x00));
        _brush.Freeze();

        _view.LayoutChanged += OnLayoutChanged;
    }

    private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
        foreach (ITextViewLine line in e.NewOrReformattedLines)
        {
            CreateVisuals(line);
        }
    }

    private void CreateVisuals(ITextViewLine line)
    {
        IWpfTextViewLineCollection textViewLines = _view.TextViewLines;
        string text = line.Extent.GetText();

        int index = 0;
        while ((index = text.IndexOf("TODO", index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var span = new SnapshotSpan(_view.TextSnapshot, line.Start + index, 4);
            Geometry geometry = textViewLines.GetMarkerGeometry(span);

            if (geometry != null)
            {
                var drawing = new GeometryDrawing(_brush, null, geometry);
                drawing.Freeze();

                var drawingImage = new DrawingImage(drawing);
                drawingImage.Freeze();

                var image = new Image
                {
                    Source = drawingImage,
                    Width = geometry.Bounds.Width,
                    Height = geometry.Bounds.Height,
                };

                Canvas.SetLeft(image, geometry.Bounds.Left);
                Canvas.SetTop(image, geometry.Bounds.Top);

                _layer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    span,
                    tag: null,
                    adornment: image,
                    removedCallback: null);
            }

            index += 4;
        }
    }
}
```

### Adornment positioning behaviors

| Behavior | Description |
|----------|-------------|
| `AdornmentPositioningBehavior.TextRelative` | Moves with the text it's attached to (most common) |
| `AdornmentPositioningBehavior.ViewportRelative` | Fixed position in the viewport (e.g., watermarks) |
| `AdornmentPositioningBehavior.OwnerControlled` | You manage the position manually |

### Layer ordering

Use `[Order]` to control where your adornment renders relative to built-in layers:

| Layer | Description |
|-------|-------------|
| `PredefinedAdornmentLayers.Selection` | The selection highlight |
| `PredefinedAdornmentLayers.Text` | The actual text glyphs |
| `PredefinedAdornmentLayers.Caret` | The caret |
| `PredefinedAdornmentLayers.CurrentLineHighlighter` | Current line highlight |
| `PredefinedAdornmentLayers.TextMarker` | Text marker decorations |

### Viewport-relative adornment (watermark example)

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace MyExtension;

internal sealed class WatermarkAdornment
{
    private const string AdornmentLayerName = "MyWatermarkAdornment";
    private readonly IAdornmentLayer _layer;
    private readonly IWpfTextView _view;
    private readonly UIElement _watermark;

    public WatermarkAdornment(IWpfTextView view)
    {
        _view = view;
        _layer = view.GetAdornmentLayer(AdornmentLayerName);

        _watermark = new TextBlock
        {
            Text = "DRAFT",
            FontSize = 48,
            Foreground = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0x00, 0x00)),
            IsHitTestVisible = false,
        };

        _view.ViewportHeightChanged += (s, e) => Render();
        _view.ViewportWidthChanged += (s, e) => Render();

        Render();
    }

    private void Render()
    {
        _layer.RemoveAllAdornments();

        Canvas.SetLeft(_watermark, _view.ViewportRight - 200);
        Canvas.SetTop(_watermark, _view.ViewportTop + 10);

        _layer.AddAdornment(
            AdornmentPositioningBehavior.ViewportRelative,
            null,
            null,
            _watermark,
            null);
    }
}
```

### Key points

- Adornments are WPF `UIElement` instances drawn on an `IAdornmentLayer`.
- Always freeze brushes and drawings for performance.
- Subscribe to `LayoutChanged` for text-relative adornments to redraw on scroll/edit.
- Set `IsHitTestVisible = false` on adornments that should not intercept mouse clicks.
- The `[ContentType]` attribute controls which file types activate your adornment (e.g., `"CSharp"`, `"text"`, `"code"`).
- The `[TextViewRole]` attribute limits which editor instances trigger your listener (use `PredefinedTextViewRoles.Document` to avoid triggering in peek, diff, etc.).
- **Remember to add the MEF asset type to your `.vsixmanifest`** — see the top of this document.

---

## Key guidance

- **VisualStudio.Extensibility** does not support custom editor adornments.
- **VSSDK / Community Toolkit** — Use `IWpfTextViewCreationListener` with MEF exports. Define an `AdornmentLayerDefinition`, subscribe to `LayoutChanged`, and draw WPF elements on the layer.
- Always declare the **MEF component asset type** in `source.extension.vsixmanifest` or the adornment will silently fail to load.
- Use `ContentType` and `TextViewRole` to scope your adornment to the right editors.
- Adornments run on the UI thread — keep drawing logic fast.

## Troubleshooting

- **Adornment doesn't appear at all:** Check the MEF asset type in `.vsixmanifest` (the #1 cause). Also verify `[ContentType]` matches the file type you're testing with and `[TextViewRole]` isn't filtering out the editor instance.
- **Adornment appears but in the wrong position:** Verify you're using `textViewLines.GetCharacterBounds()` or `GetTextMarkerGeometry()` to calculate positions. Character positions change on scroll and view resize — recalculate in `LayoutChanged`.
- **Editor becomes sluggish after adding adornment:** Your `LayoutChanged` handler is doing too much work. Pre-compute data on a background thread and only draw from cache during layout.
- **Adornment steals mouse clicks / can't select text underneath:** Set `IsHitTestVisible = false` on decorative (non-interactive) adornment elements.
- **Memory leak — VS memory grows with each file opened:** You're not unsubscribing from text view events. Subscribe to `ITextView.Closed` to remove your handlers.

## What NOT to do

> **Do NOT** forget `IsHitTestVisible = false` on decorative adornment elements — without it, they steal mouse clicks and selections from the editor text underneath.

> **Do NOT** forget to unsubscribe from `ITextView.LayoutChanged` when the view closes — subscribe to `ITextView.Closed` and remove handlers there. Leaked subscriptions cause memory leaks.

> **Do NOT** forget the `MefComponent` asset type in `.vsixmanifest` — without it, your `IWpfTextViewCreationListener` is silently ignored (#1 cause of "my adornment doesn't show up").

> **Do NOT** do expensive work in `LayoutChanged` — it fires on every scroll, resize, and edit. Pre-compute data on a background thread; only read from cache during layout.

> **Do NOT** attempt to use VisualStudio.Extensibility for editor adornments — it doesn't support them. The VSSDK in-process MEF approach is the only option.

## See also

- [vs-editor-classifier](../adding-editor-classifiers/SKILL.md)
- [vs-editor-tagger](../creating-editor-taggers/SKILL.md)
- [vs-editor-margin](../adding-editor-margins/SKILL.md)
- [vs-editor-text-view-listener](../listening-text-view-events/SKILL.md)
- [vs-theming](../theming-extension-ui/SKILL.md)

## References

- [Creating an Editor Adornment (VSSDK)](https://learn.microsoft.com/visualstudio/extensibility/walkthrough-creating-a-view-adornment-commands-and-settings)
- [IWpfTextViewCreationListener](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.text.editor.iwpftextviewcreationlistener)
- [AdornmentLayerDefinition](https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.text.editor.adornmentlayerdefinition)

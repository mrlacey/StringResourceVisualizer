// <copyright file="MyLineTransformSourceProvider.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace StringResourceVisualizer
{
    [Export(typeof(ILineTransformSourceProvider))]
#pragma warning disable SA1133 // Do not combine attributes
    [ContentType("CSharp"), ContentType("Basic"), ContentType("Razor"), ContentType("RazorCSharp"), ContentType("RazorCoreCSharp")]
#pragma warning restore SA1133 // Do not combine attributes
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal class MyLineTransformSourceProvider : ILineTransformSourceProvider
    {
        ILineTransformSource ILineTransformSourceProvider.Create(IWpfTextView view)
        {
            ResourceAdornmentManager manager = view.Properties.GetOrCreateSingletonProperty<ResourceAdornmentManager>(() => new ResourceAdornmentManager(view));
            return new MyLineTransformSource(manager);
        }
    }
}

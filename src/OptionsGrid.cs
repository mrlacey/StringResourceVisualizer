// <copyright file="OptionsGrid.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StringResourceVisualizer
{
    public class OptionsGrid : DialogPage
    {
        [DisplayName("Preferred culture")]
        [Description("Specify a culture to use in preference to the default.")]
        public string PreferredCulture { get; set; } = string.Empty;
    }
}

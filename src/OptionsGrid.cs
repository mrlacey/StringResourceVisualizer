// <copyright file="OptionsGrid.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StringResourceVisualizer
{
    public class OptionsGrid : DialogPage
    {
        [DisplayName("Preferred culture")]
        [Description("Specify a culture to use in preference to the default.")]
        public string PreferredCulture { get; set; } = string.Empty;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Settings page has been closed.
            // Prompt to reload resources in case of changes.
            Messenger.RequestReloadResources();
        }
    }
}

// <copyright file="GeneralOutputPane.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StringResourceVisualizer
{
    public class GeneralOutputPane
    {
        private static GeneralOutputPane instance;

        private readonly IVsOutputWindowPane generalPane;

        private GeneralOutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow
             && (ErrorHandler.Failed(outWindow.GetPane(ref generalPaneGuid, out this.generalPane)) || this.generalPane == null))
            {
                if (ErrorHandler.Failed(outWindow.CreatePane(ref generalPaneGuid, "General", 1, 0)))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create the Output window pane.");
                    return;
                }

                if (ErrorHandler.Failed(outWindow.GetPane(ref generalPaneGuid, out this.generalPane)) || (this.generalPane == null))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get access to the Output window pane.");
                }
            }
        }

        public static GeneralOutputPane Instance => instance ?? (instance = new GeneralOutputPane());

        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.generalPane?.Activate();
        }

        public void WriteLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.generalPane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}

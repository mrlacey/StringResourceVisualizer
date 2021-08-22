// <copyright file="OutputPane.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    public class OutputPane
    {
        private static Guid dsPaneGuid = new Guid("C2E704BD-CCBB-4817-83EB-18422895EFC4");

        private static OutputPane instance;

        private readonly IVsOutputWindowPane pane;

        private OutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow
                && (ErrorHandler.Failed(outWindow.GetPane(ref dsPaneGuid, out this.pane)) || this.pane == null))
            {
                if (ErrorHandler.Failed(outWindow.CreatePane(ref dsPaneGuid, "String Resource Visualizer", 1, 0)))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create output pane.");
                    return;
                }

                if (ErrorHandler.Failed(outWindow.GetPane(ref dsPaneGuid, out this.pane)) || (this.pane == null))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get output pane.");
                }
            }
        }

        public static OutputPane Instance => instance ?? (instance = new OutputPane());

        public async Task ActivateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            this.pane?.Activate();
        }

        public async Task WriteAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            this.pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}

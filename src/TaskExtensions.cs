// <copyright file="TaskExtensions.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    internal static class TaskExtensions
    {
        internal static void LogAndForget(this Task task, string source) =>
            task.ContinueWith(
                (t, s) => VsShellUtilities.LogError(s as string, t.Exception?.ToString()),
                source,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

        // Use the helper library (instead of TaskScheduler.Default) when drop support for VS2017
        ////VsTaskLibraryHelper.GetTaskScheduler(VsTaskRunContext.UIThreadNormalPriority));
    }
}

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StringResourceVisualizer
{
    static class TaskExtensions
    {
        internal static void LogAndForget(this Task task, string source) =>
            task.ContinueWith((t, s) => VsShellUtilities.LogError(s as string, t.Exception.ToString()),
                source,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                VsTaskLibraryHelper.GetTaskScheduler(VsTaskRunContext.UIThreadNormalPriority));
    }
}

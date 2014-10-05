using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Async.Tests
{
    public sealed class NullTaskScheduler : TaskScheduler
    {
        private readonly List<Task> _queuedTasks = new List<Task>();

        public NullTaskScheduler()
        {
            Trace.WriteLine(".ctor", "NullTaskScheduler");
        }

        protected override void QueueTask([NotNull] Task task)
        {
            if (task == null) throw new ArgumentNullException("task");
            _queuedTasks.Add(task);
            Trace.WriteLine(String.Format("Task {0} queued.", task.Id), "NullTaskScheduler");
        }

        protected override bool TryExecuteTaskInline([NotNull] Task task, bool taskWasPreviouslyQueued)
        {
            if (task == null) throw new ArgumentNullException("task");

            Trace.WriteLine(String.Format("Try execute task {0} inline. Previously queued: {1}",
                task.Id, taskWasPreviouslyQueued), "NullTaskScheduler");

            // do we have a runspace?
            if (Runspace.DefaultRunspace != null)
            {
                // ... invoke
                if (taskWasPreviouslyQueued)
                {
                    _queuedTasks.Remove(task);
                }
                return true; // task was executed inline (should it be removed now?)
            }
            
            Trace.WriteLine(String.Format("No runspace on tid: {0:x4}", Thread.CurrentThread.ManagedThreadId));
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            Trace.WriteLine("GetScheduledTasks()", "NullTaskScheduler");
            return _queuedTasks;
        }
    }
}

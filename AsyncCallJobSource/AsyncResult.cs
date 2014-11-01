using System;
using System.Diagnostics;
using System.Threading;

namespace Nivot.PowerShell.Async
{
    public class AsyncResultNoResult : IAsyncResult
    {
        private static readonly object SyncLock = new object();
        // Fields set at construction which never change while
        // operation is pending
        private readonly AsyncCallback _asyncCallback;
        private readonly object _asyncState;

        // Fields set at construction which do change after
        // operation completes
        private const int StatePending = 0;
        private const int StateCompletedSynchronously = 1;
        private const int StateCompletedAsynchronously = 2;
        
        private int _completedState = StatePending;

        // Field that may or may not get set depending on usage
        private ManualResetEventSlim _resetEvent;

        // Fields set when operation completes
        private Exception _exception;

        public AsyncResultNoResult(AsyncCallback asyncCallback, object state)
        {
            _asyncCallback = asyncCallback;
            _asyncState = state;
        }

        protected static void Log(string format, params object[] parameters)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            string message = String.Format(format, parameters);
            Trace.WriteLine(String.Format("[tid: {0:x4}] {1}", tid, message), "AsyncResult");
        }

        public static AsyncResultNoResult CreateWithEmptyCallback(object state)
        {
            return new AsyncResultNoResult(_ => { }, state);
        }

        public void SetAsCompleted(Exception exception, bool completedSynchronously)
        {
            Log("SetAsCompleted - exception: {0}; completedSynchronously: {1}",
                exception, completedSynchronously);

            // Passing null for exception means no error occurred.
            // This is the common case
            _exception = exception;

            // The _completedState field MUST be set prior calling the callback
            int prevState = Interlocked.Exchange(ref _completedState, completedSynchronously ?
                StateCompletedSynchronously : StateCompletedAsynchronously);

            if (prevState != StatePending)
            {
                throw new InvalidOperationException("You can set a result only once");
            }

            // If the event exists, set it
            if (_resetEvent != null)
            {
                Log("Set()");
                _resetEvent.Set();
            }
            // If a callback method was set, call it
            if (_asyncCallback != null)
            {
                Log("Callback()");
                _asyncCallback(this);
            }
        }

        public void EndInvoke()
        {
            Log("EndInvoke (enter)");

            // This method assumes that only 1 thread calls EndInvoke
            // for this object
            if (!IsCompleted)
            {
                Log("Waiting on handle...");
                // If the operation isn't done, wait for it
                AsyncWaitHandle.WaitOne();
                AsyncWaitHandle.Close();
                _resetEvent = null; // Allow early GC
            }

            // Operation is done: if an exception occured, throw it
            if (_exception != null)
            {
                Log("Exception encountered: {0}", _exception.Message);
                throw _exception;
            }
            Log("EndInvoke (exit)");
        }

        #region Implementation of IAsyncResult

        public object AsyncState
        {
            get
            {
                return _asyncState;
            }
        }

        public bool CompletedSynchronously
        {
            get
            {
                return Thread.VolatileRead(ref _completedState) == StateCompletedSynchronously;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (_resetEvent == null)
                {
                    bool done = IsCompleted;
                    var mre = new ManualResetEventSlim(done);
                    if (Interlocked.CompareExchange(ref _resetEvent, mre, null) != null)
                    {
                        // Another thread created this object's event; dispose
                        // the event we just created
                        mre.Dispose();
                    }
                    else
                    {
                        if (!done && IsCompleted)
                        {
                            // If the operation wasn't done when we created
                            // the event but now it is done, set the event
                            _resetEvent.Set();
                        }
                    }
                }
                Log("AsyncWaitHandle getter");
                return _resetEvent.WaitHandle;
            }
        }

        public bool IsCompleted
        {
            get
            {
                Log("IsCompleted?");
                return Thread.VolatileRead(ref _completedState) != StatePending;
            }
        }

        #endregion
    }

    public class AsyncResult<TResult> : AsyncResultNoResult {
        // Field set when operation completes
        private TResult _result;

        public AsyncResult(AsyncCallback asyncCallback, object state) :
            base(asyncCallback, state) { }
        
        public void SetAsCompleted(TResult result, bool completedSynchronously) {
            // Save the asynchronous operation's result
            _result = result;

            // Tell the base class that the operation completed
            // sucessfully (no exception)
            SetAsCompleted(null, completedSynchronously);
        }
        
        new public TResult EndInvoke() {
            base.EndInvoke();
            
            // Wait until operation has completed
            return _result; // Return the result (if above didn't throw)
        }
    }
}
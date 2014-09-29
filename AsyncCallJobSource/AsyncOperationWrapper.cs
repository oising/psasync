// #define SUPPORTS_WINRT
using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Windows.Foundation;

namespace Nivot.PowerShell.Async
{
#if SUPPORTS_WINRT
    internal class AsyncOperationWrapper<T> : IDisposable
    {
        private IAsyncOperation<T> _asyncOperation;

        public AsyncOperationWrapper(object asyncOperation)
        {
            if (asyncOperation == null) throw new ArgumentNullException("asyncOperation");

            this._asyncOperation = (IAsyncOperation<T>)asyncOperation;
        }

        ~AsyncOperationWrapper()
        {
            GC.SuppressFinalize(this);
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (this._asyncOperation == null) return;

            if (disposing)
            {
                this._asyncOperation.Close();
                this._asyncOperation = null;
            }
        }

        public AsyncStatus Status
        {
            get { return this._asyncOperation.Status; }
        }

        public object AwaitResult()
        {
            return this.AwaitResult(-1);
        }

        public object AwaitResult(int millisecondsTimeout)
        {
            var task = this._asyncOperation.AsTask();
            task.Wait(millisecondsTimeout);

            if (task.IsCompleted)
            {
                return task.Result;
            }
            if (task.IsFaulted)
            {
                Trace.Assert(task.Exception != null, "task.Exception != null");
                throw task.Exception;
            }

            throw new TaskCanceledException(task);
        }
    }
#endif
}
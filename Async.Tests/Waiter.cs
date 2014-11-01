using System;
using System.Threading;

namespace Nivot.PowerShell.Async
{
    public sealed class Waiter
    {
        private readonly int _millisecondsToWait; // Milliseconds;

        public Waiter(int seconds)
        {
            _millisecondsToWait = seconds*1000;
        }

        // Synchronous version of time-consuming method
        public DateTime DoWait()
        {
            Thread.Sleep(_millisecondsToWait); // Simulate time-consuming task
            
            return DateTime.Now; // Indicate when task completed
        }

        // Asynchronous version of time-consuming method (Begin part)
        public IAsyncResult BeginDoWait(AsyncCallback callback, object state)
        {
            // Create IAsyncResult object identifying the
            // asynchronous operation
            var ar = new AsyncResult<DateTime>(callback, state);

            // Use a thread pool thread to perform the operation
            ThreadPool.QueueUserWorkItem(DoWaitHelper, ar);

            return ar; // Return the IAsyncResult to the caller
        }

        public IAsyncResult BeginDoWaitNoResult(AsyncCallback callback, object state)
        {
            // Create IAsyncResult object identifying the
            // asynchronous operation
            var ar = new AsyncResultNoResult(callback, state);

            // Use a thread pool thread to perform the operation
            ThreadPool.QueueUserWorkItem(DoWaitHelperNoResult, ar);

            return ar; // Return the IAsyncResult to the caller
        }

        // Asynchronous version of time-consuming method (End part)
        public DateTime EndDoWait(IAsyncResult asyncResult)
        {
            // We know that the IAsyncResult is really an
            // AsyncResult<DateTime> object
            var ar = (AsyncResult<DateTime>) asyncResult;

            // Wait for operation to complete, then return result or
            // throw exception
            return ar.EndInvoke();
        }

        public void EndDoWaitNoResult(IAsyncResult asyncResult)
        {
            // We know that the IAsyncResult is really an
            // AsyncResult<DateTime> object
            var ar = (AsyncResultNoResult)asyncResult;

            // Wait for operation to complete, then return result or
            // throw exception
            ar.EndInvoke();
        }

        // Asynchronous version of time-consuming method (private part // to set completion result/exception)
        private void DoWaitHelper(object asyncResult)
        {
            // We know that it's really an AsyncResult<DateTime> object
            var ar = (AsyncResult<DateTime>) asyncResult;
            
            try
            {
                // Perform the operation; if sucessful set the result
                DateTime dt = DoWait();
                ar.SetAsCompleted(dt, false);
            }
            catch (Exception e)
            {
                // If operation fails, set the exception
                ar.SetAsCompleted(e, false);
            }
        }

        private void DoWaitHelperNoResult(object asyncResult)
        {
            var ar = (AsyncResultNoResult)asyncResult;

            try
            {
                DoWait();
                ar.SetAsCompleted(null, false);
            }
            catch (Exception e)
            {
                // If operation fails, set the exception
                ar.SetAsCompleted(e, false);
            }
        }
    }
}
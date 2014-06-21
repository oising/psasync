using System;
using System.Diagnostics;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Nivot.PowerShell.Async
{
    public class AsyncCallJob : Job2
    {
        private readonly CancellationToken _cancelToken;

        public AsyncCallJob(Task call, string command, string name, Guid instanceId)
            : base(command, name, instanceId)
        {
            Tracer.LogVerbose("AsyncCallJob .ctor ID:{0}", instanceId);
            this._cancelToken = new CancellationToken();
        }

        private void EnsureNotCancelled()
        {
            if (_cancelToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Cancellation was requested.", _cancelToken);
            }
        }

        public override string StatusMessage
        {
            get
            {
                return "(status)";
            }
        }

        public override bool HasMoreData
        {
            get
            {
                // true if job has completed (returns the result T of the asyncoperation<T>)
                return false;
            }
        }

        public override string Location
        {
            get
            {
                // WinRT, Desktop ?
                return "(location)";
            }
        }

        public override void StartJob()
        {
            Tracer.LogInfo("StartJob() ID: {0}", this.InstanceId);
        }

        public override void StartJobAsync()
        {
            Tracer.LogInfo("StartJobAsync() ID: {0}", this.InstanceId);
            throw new NotImplementedException();
        }

        public override void StopJob()
        {
            Tracer.LogInfo("StopJob() ID: {0}", this.InstanceId);
            throw new NotImplementedException();
        }

        public override void StopJobAsync()
        {
            Tracer.LogInfo("StopJobAsync() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void SuspendJob()
        {
            Tracer.LogInfo("SuspendJob() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void SuspendJobAsync()
        {
            Tracer.LogInfo("SuspendJobAsync() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void ResumeJob()
        {
            Tracer.LogInfo("ResumeJob() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void ResumeJobAsync()
        {
            Tracer.LogInfo("ResumeJobAsync() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void UnblockJob()
        {
            Tracer.LogInfo("UnblockJob() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void UnblockJobAsync()
        {
            Tracer.LogInfo("UnblockJobAsync() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void StopJob(bool force, string reason)
        {
            Tracer.LogInfo("StopJob() ID: {0}", this.InstanceId);
            throw new NotImplementedException();
        }

        public override void StopJobAsync(bool force, string reason)
        {
            Tracer.LogInfo("StopJobAsync() ID: {0}", this.InstanceId);
            throw new NotImplementedException();
        }

        public override void SuspendJob(bool force, string reason)
        {
            Tracer.LogInfo("SuspendJob() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }

        public override void SuspendJobAsync(bool force, string reason)
        {
            Tracer.LogInfo("SuspendJobAsync() ID: {0}", this.InstanceId);
            throw new NotSupportedException();
        }
    }
}
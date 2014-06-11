using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

using Windows.Foundation;

namespace Nivot.PowerShell.Async
{
    public class AsyncCallJobSourceAdapter : JobSourceAdapter
    {
        private readonly AsyncCallRepository _calls;

        public AsyncCallJobSourceAdapter()
        {
            this.Name = "AsyncCall";
            this._calls = new AsyncCallRepository("Async Calls");
            this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
            this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
            this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
        }

        public override Job2 NewJob(JobInvocationInfo specification)
        {
            throw new NotImplementedException();
        }

        public override IList<Job2> GetJobs()
        {
            return (
                from call in this._calls.GetItems()
                select new AsyncCallJob(null, "foo", "bar", Guid.NewGuid()))
                    .Cast<Job2>()
                    .ToList();
        }

        public override IList<Job2> GetJobsByName(string name, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override IList<Job2> GetJobsByCommand(string command, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override Job2 GetJobByInstanceId(Guid instanceId, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override Job2 GetJobBySessionId(int id, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override IList<Job2> GetJobsByState(JobState state, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override IList<Job2> GetJobsByFilter(Dictionary<string, object> filter, bool recurse)
        {
            throw new NotImplementedException();
        }

        public override void RemoveJob(Job2 job)
        {
            throw new NotImplementedException();
        }

        private class AsyncCallRepository : Repository<AsyncCallInfo>
        {
            public AsyncCallRepository(string identifier)
                : base(identifier)
            {
            }

            protected override Guid GetKey(AsyncCallInfo item)
            {
                return item.Id;
            }
        }

    }

    // .NET async awaitable
    // WinRT async awaitable
    // .NET begin/end async call

    internal class AsyncCallInfo
    {

        public AsyncCallInfo(Guid id, dynamic callSite)
        {
            this.Id = id;
            this.CallSite = callSite;
        }

        public Guid Id;

        public dynamic CallSite;

        public AsyncStatus Status;
    }
}

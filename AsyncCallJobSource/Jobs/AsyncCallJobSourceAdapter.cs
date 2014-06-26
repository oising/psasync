using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Nivot.PowerShell.Async.Jobs
{
    //public class AsyncCallJobSourceAdapter : JobSourceAdapter
    //{
    //    private readonly AsyncCallRepository _calls;

    //    public AsyncCallJobSourceAdapter()
    //    {
    //        this.Name = "AsyncCallAdapter";
    //        this._calls = new AsyncCallRepository("Async Calls");
    //        this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
    //        this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
    //        this._calls.Add(new AsyncCallInfo(Guid.NewGuid(), null));
    //    }

    //    public override Job2 NewJob(JobInvocationInfo specification)
    //    {
    //        Tracer.LogVerbose("NewJob");
    //        throw new NotImplementedException();
    //    }

    //    public override Job2 NewJob(string definitionName, string definitionPath)
    //    {
    //        Tracer.LogVerbose("NewJob name: {0}; path: {1}", definitionName, definitionPath);
    //        throw new NotImplementedException();
    //    }

    //    public override IList<Job2> GetJobs()
    //    {
    //        Tracer.LogVerbose("GetJobs()");

    //        return (
    //            from call in this._calls.GetItems()
    //            select new AsyncCallJob(null, "foo", "bar", Guid.NewGuid()))
    //                .Cast<Job2>()
    //                .ToList();
    //    }

    //    public override IList<Job2> GetJobsByName(string name, bool recurse)
    //    {
    //        Tracer.LogVerbose("GetJobsByName");

    //        throw new NotImplementedException();
    //    }

    //    public override IList<Job2> GetJobsByCommand(string command, bool recurse)
    //    {
    //        Tracer.LogVerbose("GetJobsByCommand");

    //        throw new NotImplementedException();
    //    }

    //    public override Job2 GetJobByInstanceId(Guid instanceId, bool recurse)
    //    {
    //        Tracer.LogVerbose("GetJobByInstanceId");

    //        throw new NotImplementedException();
    //    }

    //    public override Job2 GetJobBySessionId(int id, bool recurse)
    //    {
    //        Tracer.LogVerbose("getJobBySessionId");

    //        throw new NotImplementedException();
    //    }

    //    public override IList<Job2> GetJobsByState(JobState state, bool recurse)
    //    {
    //        Tracer.LogVerbose("GetJobsByState");

    //        throw new NotImplementedException();
    //    }

    //    public override IList<Job2> GetJobsByFilter(Dictionary<string, object> filter, bool recurse)
    //    {
    //        Tracer.LogVerbose("GetJobsByFilter");

    //        throw new NotImplementedException();
    //    }

    //    public override void RemoveJob(Job2 job)
    //    {
    //        Tracer.LogVerbose("RemoveJob");

    //        throw new NotImplementedException();
    //    }

    //    // TODO: this should be a job repo, not a callinfo repo
    //    private class AsyncCallRepository : Repository<AsyncCallInfo>
    //    {
    //        public AsyncCallRepository(string identifier)
    //            : base(identifier)
    //        {
    //        }

    //        protected override Guid GetKey(AsyncCallInfo item)
    //        {
    //            Tracer.LogVerbose("AsyncCallRepo GetKey {0}", item.Id);
    //            return item.Id;
    //        }
    //    }

    //}

    // .NET async awaitable
    // WinRT async awaitable
    // .NET begin/end async call
}

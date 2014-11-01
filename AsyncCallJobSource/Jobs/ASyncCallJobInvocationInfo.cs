using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.Serialization;

namespace Nivot.PowerShell.Async.Jobs
{
    [Serializable]
    public class ASyncCallJobInvocationInfo : JobInvocationInfo
    {
        protected ASyncCallJobInvocationInfo(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public ASyncCallJobInvocationInfo(JobDefinition definition, Dictionary<string, object> parameters) : base(definition, parameters)
        {
        }
    }
}
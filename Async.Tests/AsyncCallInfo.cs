using System;
using System.IO;

using Windows.Foundation;

using Microsoft.CSharp.RuntimeBinder;

namespace Nivot.PowerShell.Async
{
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
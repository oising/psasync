using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.PowerShell.Commands;

namespace Nivot.PowerShell.Async.Commands
{
    [Cmdlet(VerbsLifecycle.Register, "AsyncCallEvent")]
    public class RegisterAsyncCallEventCommand : ObjectEventRegistrationBase
    {
        [UsedImplicitly]
        public class BindingInfo
        {
            public BindingInfo()
            {
                ID = Guid.NewGuid();
            }

            public Guid ID { get; private set; }

            public string MethodName { get; set; }
        }

        private static readonly object _sync = new object();
        private static readonly ConditionalWeakTable<object, BindingInfo> _sourceObjects;

        static RegisterAsyncCallEventCommand()
        {
            _sourceObjects = new ConditionalWeakTable<object, BindingInfo>();
        }

        private BindingInfo GetBindingInfo<T>(T reference) where T : class
        {
            lock (_sync)
            {
                return _sourceObjects.GetOrCreateValue(reference);
            }
        }

        protected override void BeginProcessing()
        {
            if (this.MaxTriggerCount > 1)
            {
                throw new PSArgumentOutOfRangeException("MaxTriggerCount",
                    MaxTriggerCount, "The maximum trigger count for a callback event is fixed at 1.");
            }

            EnsureAsynchronousMethodProvided();

            base.BeginProcessing();
        }

        private void EnsureAsynchronousMethodProvided()
        {
            if (InputObject == null)
            {
                throw new PSArgumentNullException("InputObject", "InputObject may not be null.");
            }

            bool isStatic = false;
            Type targetType;
            if (InputObject.BaseObject is Type)
            {
                targetType = (Type)InputObject.BaseObject;
                isStatic = true;
            }
            else
            {
                targetType = InputObject.BaseObject.GetType();
            }

            // Func<T1, Tn..., AsyncCallback, object, IAsyncResult> begin,
            // Func<IAsyncResult, dynamic> end)
            // begin/end?
            if (MethodName.StartsWith("Begin"))
            {
                var args = ArgumentList ?? new Type[0];
                var signature = new Type[args.Length + 2];
                Type.GetTypeArray(args).CopyTo(signature, index: 0);
                (new[] { typeof(AsyncCallback), typeof(object)}).CopyTo(signature, args.Length);
                
                var openFunc = typeof(Func<>);
                
                MethodInfo begin = targetType.GetMethod(MethodName, signature);
                var beginFuncSignature = new Type[signature.Length + 1];
                signature.CopyTo(beginFuncSignature, 0);
                beginFuncSignature[signature.Length] = typeof(IAsyncResult); // return
                var beginHandler = begin.CreateDelegate(openFunc.MakeGenericType(beginFuncSignature), targetType);

                MethodInfo end = targetType.GetMethod(
                    MethodName.Replace("Begin", "End"),
                    new[] { typeof(IAsyncResult) });
                var endFuncSignature = new Type[2] { typeof(IAsyncResult), end.ReturnType };
                var endHandler = end.CreateDelegate(openFunc.MakeGenericType(endFuncSignature), targetType);


                // BeginFoo(arg1, arg2, argN, AsyncCallback, object) : IAsyncResult // ..., callback, state
                //var result = (IAsyncResult)beginHandler.DynamicInvoke( /*...*/, , null);
                //var factory = Task<dynamic>.Factory.FromAsync()
                //factory.
            }
            else if (MethodName.EndsWith("Async"))
            {
                this.WriteWarning("*Async method handling not implemented.");
            }
            // winrt / task?

            // 
        }

        protected override void EndProcessing()
        {
            //this.SourceIdentifier = this.GetBindingInfo(InputObject).ID.ToString();

            base.EndProcessing();
        }

        protected override object GetSourceObject()
        {
            this.MaxTriggerCount = 1;
            this.WriteVerbose(String.Format("GetSourceObject(); ID is {0}",
                this.GetBindingInfo(InputObject).ID));
            
            return this.InputObject;
        }

        protected override string GetSourceObjectEventName()
        {
            this.WriteVerbose("GetSourceObjectEventName()");
            return this.MethodName;
            //throw new NotImplementedException();
        }

        [Parameter(Mandatory = true, Position = 0)]
        public PSObject InputObject { get; set; }

        //Func<T1, AsyncCallback, object, IAsyncResult> begin,
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string MethodName { get; set; }

        //Func<IAsyncResult, dynamic> end)
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public object[] ArgumentList { get; set; }
    }
}

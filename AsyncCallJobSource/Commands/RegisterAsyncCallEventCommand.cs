using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerShell.Commands;

namespace Nivot.PowerShell.Async.Commands
{
    [Cmdlet(VerbsLifecycle.Register, "AsyncCallEvent",
        DefaultParameterSetName = PARAMSET_APM,
        SupportsShouldProcess = true)]
    public class RegisterAsyncCallEventCommand : ObjectEventRegistrationBase
    {
        private const string PARAMSET_APM = "APM";
        private const string PARAMSET_EAP = "EAP";
        private const string PARAMSET_TAP = "TAP";

        private static readonly object Sync = new object();
        private static readonly ConditionalWeakTable<object, PSPromise> SourceObjectTable;

        private object _baseObject;

        static RegisterAsyncCallEventCommand()
        {
            SourceObjectTable = new ConditionalWeakTable<object, PSPromise>();
        }

        private PSPromise GetBindingInfo<T>(T reference) where T : class
        {
            lock (Sync)
            {
                return SourceObjectTable.GetOrCreateValue(reference);
            }
        }

        protected override void BeginProcessing()
        {
            if (this.MaxTriggerCount > 1)
            {
                throw new PSArgumentOutOfRangeException("MaxTriggerCount",
                    MaxTriggerCount, "The maximum trigger count for a callback event is fixed at 1.");
            }

            base.BeginProcessing();

            _baseObject = InputObject.BaseObject;
        }

        private void ProcessCallbackBinding()
        {
            // Trying to roughly follow http://msdn.microsoft.com/en-us/library/ms228974(v=vs.110).aspx
            // "Event-based async pattern"

            if (InputObject == null)
            {
                throw new PSArgumentNullException("InputObject", "InputObject may not be null.");
            }

            bool isStatic = false;
            Type targetType;
            if ((_baseObject as Type) != null)
            {
                targetType = (Type)_baseObject;
                isStatic = true;
                this.WriteVerbose("InputObject is a Type: " + targetType.Name);
            }
            else
            {
                targetType = _baseObject.GetType();
                this.WriteVerbose("InputObject is an instance of " + targetType.Name);
            }

            // Func<T1, Tn..., AsyncCallback, object, IAsyncResult> begin,
            // Func<IAsyncResult, dynamic> end)
            // begin/end?
            if (MethodName.StartsWith("Begin",
                StringComparison.OrdinalIgnoreCase))
            {
                WriteVerbose("Method is AsyncCallback Begin/End pairing style.");

                string verb = MethodName.Substring(5);
                string endMethodName = "End" + verb;
                MethodInfo endMethod = targetType.GetMethod(endMethodName, new[] {typeof (IAsyncResult)});

                if (endMethod == null)
                {
                    throw new PSArgumentException(String.Format(
                        "No matching '{0}(IAsyncResult)' method found for APM call '{1}'.",
                        endMethodName, MethodName));
                    
                    // TODO: throw proper terminating error
                    //this.ThrowTerminatingError(new ErrorRecord());
                }
                //BindBeginEndStyleMethod(targetType, isStatic);
            }
            else if (MethodName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                // determine if EAP or TAP mode call
                string verb = MethodName.Substring(0, MethodName.Length - 5); // e.g. "[Read]Async"

                this.WriteWarning("*Async method handling not implemented, yet.");
            }
            // winrt / task?

            // 
        }


        protected override void EndProcessing()
        {
            // hook up everything first
            base.EndProcessing();
            
            this.ProcessCallbackBinding();
        }

        protected override object GetSourceObject()
        {
            // after the callback completes, we want it to auto-unregister
            this.MaxTriggerCount = 1;

            PSPromise binding = this.GetBindingInfo(_baseObject);
            
            this.WriteVerbose(String.Format("GetSourceObject(); ID is {0}", binding.Id));

            // the PSEventJob will monitor for the Completed
            // event on this instance.
            return binding; 
        }

        protected override string GetSourceObjectEventName()
        {
            string eventName = String.Format("{0}Completed", MethodName.Substring(5)); // trim "Begin"
            
            // subscription is unique per instance/event combo (this might be too restrictive?)
            Guid id = this.GetBindingInfo(_baseObject).Id;
            this.SourceIdentifier = (id + "_" + eventName);
            
            this.WriteVerbose("GetSourceObjectEventName(): " + this.SourceIdentifier);
            
            return "Completed"; // fixed-name event for PSPromise
        }

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        public PSObject InputObject { get; set; }

        //Func<T1, AsyncCallback, object, IAsyncResult> begin,
        [Parameter(Mandatory = true, Position=0)]
        [ValidateNotNullOrEmpty]
        public string MethodName { get; set; }

        //Func<IAsyncResult, dynamic> end)
        [Parameter(Mandatory = false, ValueFromRemainingArguments = true)]
        //[ValidateNotNull]
        public object[] ArgumentList { get; set; }
    }

    internal abstract partial class PSTaskFactory
    {
        internal static Task FromAsyncMethod(object target, string methodName, object[] parameters)
        {
            return null;
        }

        internal static Task<TResult> FromAsyncMethod<TResult>(object target, string methodName, object[] parameters)
        {
            return null;
        }

        private class APMTaskFactory
        {
            private void BindBeginEndStyleMethod(Type targetType, bool isStatic)
            {
                var args = ArgumentList ?? new Type[0];
                var beginSignature = new Type[args.Length + 2];
                Type.GetTypeArray(args).CopyTo(beginSignature, index: 0); // FIXME: null arg will throw
                (new[] { typeof(AsyncCallback), typeof(object) }).CopyTo(beginSignature, args.Length);

                // Begin method has a variable parameter count of between 0 and 4 extra (on top of asyncallback and state.)
                var beginHandler = this.BuildBeginHandler(targetType, beginSignature, isStatic);

                // End method has a fixed parameter count of 1.
                bool isVoid;
                var endHandler = this.BuildEndHandler(targetType, isStatic, out isVoid);

                var binding = this.GetBindingInfo(_baseObject);

                // BeginFoo(arg1, arg2, argN, AsyncCallback, object) : IAsyncResult // ..., callback, state
                Action<IAsyncResult> callback = result =>
                {
                    Tracer.LogVerbose("Callback for: {0}", this.SourceIdentifier);
                    // raise event on PSPromise
                    object retVal = null;
                    if (!isVoid)
                    {
                        retVal = endHandler.DynamicInvoke(result);
                        Tracer.LogInfo("Retval: {0}", (retVal == null) ? "<null>" : retVal.GetType().Name);
                    }
                    else
                    {
                        endHandler.DynamicInvoke(result);
                    }
                    // TODO: construct a custom EA
                    binding.OnCompleted(new AsyncCompletedEventArgs(null, false, retVal));
                };

                var beginParameters = new object[args.Length + 2];
                args.CopyTo(beginParameters, 0);
                beginParameters[args.Length] = new AsyncCallback(callback);
                beginParameters[args.Length + 1] = null; // object state

                //Tracer.LogInfo("Invoking Begin Handler");
                //this.WriteVerbose("Invoking Begin Handler");
                //var result2 = (IAsyncResult)beginHandler.DynamicInvoke(beginParameters);

                // create Type array for signature
                var factorySignature = new Type[args.Length + 3];
                factorySignature[0] = beginHandler.GetType();
                factorySignature[1] = endHandler.GetType();
                Type.GetTypeArray(args).CopyTo(factorySignature, 2); // FIXME: null arg will throw
                factorySignature[factorySignature.Length - 1] = typeof(object); // state



                dynamic factory = null;
                factory = isVoid ? (dynamic)Task.Factory : Task<dynamic>.Factory;

                //MethodInfo factoryHandler = factory.GetType().GetMethod("FromAsync", factorySignature);
                //Debug.Assert(factoryHandler != null, "factoryHandler != null");

                // create object array for parameters
                var factoryParameters = new object[args.Length + 3];
                factoryParameters[0] = beginHandler;
                factoryParameters[1] = endHandler;
                args.CopyTo(factoryParameters, 2);
                factoryParameters[factoryParameters.Length - 1] = null;

                switch (args.Length)
                {
                    case 0:
                        break;
                    case 1:
                        break;
                    case 2:
                        break;
                    case 3:
                        break;
                    default:
                        break;
                }
                // write task to output
                //this.WriteObject(factoryHandler.Invoke(factory, factoryParameters));

                //factory
                //WriteObject(result2);
            }

            private Delegate BuildEndHandler(Type targetType, bool isStatic, out bool isVoid)
            {
                BindingFlags flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
                flags |= isStatic ? BindingFlags.Static : BindingFlags.Instance;

                MethodInfo end = targetType.GetMethod(
                    this.MethodName.Replace("Begin", "End"),
                    flags,
                    null,
                    new[] { typeof(IAsyncResult) },
                    null);

                if (end == null)
                {
                    throw new PSArgumentException("Could not find matching end method.");
                }

                Type[] endFuncSignature;
                Type closeFunc; // (IAsyncResult) => dynamic (end.ReturnType) || void
                isVoid = false;
                if (end.ReturnType == typeof(void))
                {
                    this.WriteVerbose("End is void, building Action<>");
                    closeFunc = typeof(Action<>); // void return
                    endFuncSignature = new[] { typeof(IAsyncResult) };
                    isVoid = true;
                }
                else
                {
                    this.WriteVerbose("End is non-void, building Func<,>");
                    closeFunc = typeof(Func<,>);
                    endFuncSignature = new[] { typeof(IAsyncResult), end.ReturnType };
                }
                var endHandler = end.CreateDelegate(
                    closeFunc.MakeGenericType(endFuncSignature),
                    isStatic ? targetType : this._baseObject);

                return endHandler;
            }

            private Delegate BuildBeginHandler(Type targetType, Type[] signature, bool isStatic)
            {
                Type openFunc;
                switch (signature.Length - 2) // substract asynccallback/state params
                {
                    case 0:
                        openFunc = typeof(Func<,,>); // (AsyncCallback, object) => IAsyncResult
                        break;
                    case 1:
                        openFunc = typeof(Func<,,,>); // (T1, AsyncCallback, object) => IAsyncResult
                        break;
                    case 2:
                        openFunc = typeof(Func<,,,,>); // (T1, T2, AsyncCallback, object) => IAsyncResult
                        break;
                    case 3:
                        openFunc = typeof(Func<,,,,,>); // (T1, T2, T3, AsyncCallback, object) => IAsyncResult
                        break;
                    case 4:
                        openFunc = typeof(Func<,,,,,,>); // (T1, T2, T3, T4, AsyncCallback, object) => IAsyncResult
                        break;
                    default:
                        throw new PSArgumentException("Signature has too many parameters. " +
                            " The maximum supported is 4 custom plus AsyncCallback/object for a total of 6.",
                            "ArgumentList");
                }

                BindingFlags flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
                flags |= isStatic ? BindingFlags.Static : BindingFlags.Instance;

                MethodInfo begin = targetType.GetMethod(
                    this.MethodName,
                    flags,
                    null,
                    signature,
                    null);

                if (begin == null)
                {
                    throw new PSArgumentException(
                        "Could not find a matching method for the argument list provided.",
                        "ArgumentList");
                }

                this.WriteVerbose("Overload is: " + begin.ToString());

                var beginFuncSignature = new Type[signature.Length + 1];
                signature.CopyTo(beginFuncSignature, 0);
                beginFuncSignature[signature.Length] = typeof(IAsyncResult); // return
                var beginHandler = begin.CreateDelegate(
                    openFunc.MakeGenericType(beginFuncSignature),
                    isStatic ? targetType : this._baseObject);

                return beginHandler;
            }
            
        }

        private class EAPTaskFactory
        {
            
        }
    }
}

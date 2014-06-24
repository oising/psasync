﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    [UsedImplicitly]
    public class BindingInfo
    {
        private readonly object _sync = new Object();

        private event EventHandler<AsyncCompletedEventArgs> CompletedInternal;

        public BindingInfo()
        {
            ID = Guid.NewGuid();
            Tracer.LogVerbose("New BindingInfo() {0}", ID);

            CompletedInternal = delegate
            {
                Tracer.LogInfo("Raising Completed for {0}; subscribers: {1}",
                    this.ID, CompletedInternal.GetInvocationList().Length);
                
            }; // empty
        }

        internal void OnCompleted(AsyncCompletedEventArgs args)
        {
            var temp = CompletedInternal;
            if (temp != null)
            {
                temp(this, args);
            }
        }

        public Guid ID { get; private set; }

        public string MethodName { get; internal set; }

        public event EventHandler<AsyncCompletedEventArgs> Completed
        {
            add
            {
                lock (_sync)
                {
                    Tracer.LogVerbose("add_Completed");
                    CompletedInternal += value;
                }
            }
            remove
            {
                lock (_sync)
                {
                    Tracer.LogVerbose("remove_Completed");
                    CompletedInternal -= value;
                    Debug.Assert(CompletedInternal.GetInvocationList().Length == 1); // account for empty delegate
                }
            }
        }
    }
    [Cmdlet(VerbsLifecycle.Register, "AsyncCallEvent")]
    public class RegisterAsyncCallEventCommand : ObjectEventRegistrationBase
    {
        private static readonly object _sync = new object();
        private static readonly ConditionalWeakTable<object, BindingInfo> _sourceObjects;

        private object _baseObject;

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

            base.BeginProcessing();

            _baseObject = InputObject.BaseObject;
        }

        private void EnsureAsynchronousMethodProvided()
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
            if (MethodName.StartsWith("Begin"))
            {
                WriteVerbose("Method is AsyncCallback Begin/End pairing style.");

                var args = ArgumentList ?? new Type[0];
                var signature = new Type[args.Length + 2];
                Type.GetTypeArray(args).CopyTo(signature, index: 0);
                (new[] { typeof(AsyncCallback), typeof(object)}).CopyTo(signature, args.Length);

                // This is fugly, but the alterative is to use expression trees which are painful [for me] to write.
                Type openFunc;
                switch (args.Length)
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
                        throw new ArgumentException("Signature has too many parameters. The maximum supported is 4 custom plus AsyncCallback/object for a total of 6.");
                }
                
                // Begin method has a variable parameter count of between 0 and 4 extra (on top of asyncallback and state.)
                MethodInfo begin = targetType.GetMethod(MethodName, signature);
                if (begin == null)
                {
                    throw new ArgumentOutOfRangeException("ArgumentList",
                        "Could not find a matching method for the argument list provided.");
                }

                this.WriteVerbose("Overload is: " + begin.ToString());

                var beginFuncSignature = new Type[signature.Length + 1];
                signature.CopyTo(beginFuncSignature, 0);
                beginFuncSignature[signature.Length] = typeof(IAsyncResult); // return
                var beginHandler = begin.CreateDelegate(openFunc.MakeGenericType(beginFuncSignature),
                    isStatic ? targetType : _baseObject);

                // End method has a fixed parameter count of 1.
                MethodInfo end = targetType.GetMethod(
                    MethodName.Replace("Begin", "End"),
                    new[] { typeof(IAsyncResult) });
                if (end == null)
                {
                    throw new ArgumentException("Could not find matching end method.");
                }

                Type[] endFuncSignature;
                Type closeFunc; // (IAsyncResult) => dynamic (end.ReturnType) || void
                bool isVoid = false;
                if (end.ReturnType == typeof(void))
                {
                    this.WriteVerbose("End is void, building Action<>");
                    closeFunc = typeof(Action<>); // void return
                    endFuncSignature = new Type[1] { typeof(IAsyncResult) };
                    isVoid = true;
                }
                else
                {
                    this.WriteVerbose("End is non-void, building Func<,>");
                    closeFunc = typeof(Func<,>);
                    endFuncSignature = new Type[2] { typeof(IAsyncResult), end.ReturnType };
                }
                var endHandler = end.CreateDelegate(closeFunc.MakeGenericType(endFuncSignature),
                    isStatic ? targetType : _baseObject);

                var binding = this.GetBindingInfo(_baseObject);

                // BeginFoo(arg1, arg2, argN, AsyncCallback, object) : IAsyncResult // ..., callback, state
                Action<IAsyncResult> callback = result =>
                    {
                        Tracer.LogVerbose("Callback for: {0}", this.SourceIdentifier);
                        // raise event on BindingInfo
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
                        binding.OnCompleted(new AsyncCompletedEventArgs(null, false, retVal)); // TODO: construct a custom EA
                    };
                
                var parameters = new object[args.Length + 2];
                args.CopyTo(parameters, 0);
                parameters[args.Length] = new AsyncCallback(callback);
                parameters[args.Length + 1] = null; // object state

                Tracer.LogInfo("Invoking Begin Handler");
                this.WriteVerbose("Invoking Begin Handler");
                var result2 = (IAsyncResult)beginHandler.DynamicInvoke(parameters);

                // TODO: construct Task<> from begin/end pairing to abstract async method patterns
                //var factory = Task<dynamic>.Factory.FromAsync()
                //factory
                WriteObject(result2);
            }
            else if (MethodName.EndsWith("Async"))
            {
                this.WriteWarning("*Async method handling not implemented, yet.");
            }
            // winrt / task?

            // 
        }

        protected override void EndProcessing()
        {
            // hook up everything first
            base.EndProcessing();

            //var binding = this.GetBindingInfo(_baseObject);
            
            this.EnsureAsynchronousMethodProvided();
        }

        protected override object GetSourceObject()
        {
            // after the callback completes, we want it to auto-unregister
            this.MaxTriggerCount = 1;

            BindingInfo binding = this.GetBindingInfo(_baseObject);
            
            this.WriteVerbose(String.Format("GetSourceObject(); ID is {0}", binding.ID));

            // the PSEventJob will monitor for the Completed
            // event on this instance.
            return binding; 
        }

        protected override string GetSourceObjectEventName()
        {
            string eventName = String.Format("{0}Completed", MethodName.Substring(5)); // trim "Begin"
            
            // subscription is unique per instance/event combo (this might be too restrictive?)
            Guid id = this.GetBindingInfo(_baseObject).ID;
            this.SourceIdentifier = (id + "_" + eventName);
            
            this.WriteVerbose("GetSourceObjectEventName(): " + this.SourceIdentifier);
            
            return "Completed"; // fixed-name event for BindingInfo
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        public PSObject InputObject { get; set; }

        //Func<T1, AsyncCallback, object, IAsyncResult> begin,
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string MethodName { get; set; }

        //Func<IAsyncResult, dynamic> end)
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public object[] ArgumentList { get; set; }
    }
}

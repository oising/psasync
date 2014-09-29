//#define USE_COMPILER_CODEGEN

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Nivot.PowerShell.Async;
using Nivot.PowerShell.Async.Commands;
using Sigil;
using Sigil.NonGeneric;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Async.Tests
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }

        private static object _syncLock = new object();

        private static void Log(string format, params object[] parameters)
            //[CallerMemberName]string caller = "(unknown)")
        {
            Trace.WriteLine(String.Format(format, parameters),
                String.Format("tid: {0:X4}", Thread.CurrentThread.ManagedThreadId));
        }

        [TestInitialize]
        public void Init()
        {
            Log("Init");
            Trace.AutoFlush = true;
        }

        [TestMethod]
        public void TestMethod1()
        {
            var task = AsyncTest.TestBeginEndPairingTyped(@"C:\temp\t.txt");
            bool didNotTimeOut = task.Wait(new TimeSpan(0, 0, seconds: 5));
            Assert.IsTrue(didNotTimeOut);
            Assert.IsTrue(task.IsCompleted && (!task.IsFaulted));
        }

        [TestMethod]
        public void TestMethod2()
        {
            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin = (bytes, i, arg3, arg4, arg5) => null;
            Func<IAsyncResult, dynamic> end = result => 42;

            var data = new byte[] {1, 2, 3, 4, 5};

            Expression<Func<IAsyncResult, Task<dynamic>>> expr =
                result => Task<dynamic>.Factory.FromAsync(begin, end, data, 0, 100, null);

            Task<dynamic> meaning = expr.Compile()(null);

        }

        [TestMethod]
        public void TestMethod3()
        {
            Log("Test3.Enter");

            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin = (bytes, i, arg3, arg4, arg5) =>
            {
                Log("Begin.Enter");
                //var ar = new SleepingAsyncResultVoid(arg4, arg5, this, "begin");

                ThreadPool.QueueUserWorkItem(state =>
                {
                    Log("Begin.Sleep");
                    Thread.Sleep(500);

                    Log("Begin.Signalled");
                });
                Log("Begin.Exit");

                //return ar;
                return null;
            };
            Func<IAsyncResult, dynamic> end = result =>
            {
                Log("End.Enter");
                return 42;
            };
            var data = new byte[] {1, 2, 3, 4, 5};

            var factory = Task<dynamic>.Factory;
            var task = factory.FromAsync(begin, end, data, 0, 100, null);
            Log("Waiting for task");
            task.Wait(millisecondsTimeout: 1000);
            Log("Retrieving result");
            dynamic taskResult = task.Result;
            Log("Result was {0}", taskResult);
            Log("Test3.Exit");
        }

        public void TestMethod4()
        {
            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin = (bytes, i, arg3, arg4, arg5) => null;
            Func<IAsyncResult, StringBuilder> end = result => new StringBuilder();
            var data = new byte[] {1, 2, 3, 4, 5};

            dynamic factory = Task<StringBuilder>.Factory;
            var task = factory.FromAsync(begin, end, data, 0, 100, null);
        }

        [TestMethod]
        public void TestMethod5()
        {
            var w = new Waiter(seconds: 1);
            var factory = Task<DateTime>.Factory;
            var task = factory.FromAsync(w.BeginDoWait, w.EndDoWait, null);
            task.Wait(millisecondsTimeout: 1000);
            var result = task.Result;
            Log("result {0}", result);
        }

        [TestMethod]
        public void TestMethod6()
        {
            var iss = InitialSessionState.CreateDefault();
            iss.Commands.Add(
                new SessionStateCmdletEntry(
                    "Register-AsyncCallEvent",
                    typeof (RegisterAsyncCallEventCommand),
                    helpFileName: null));

            var w = new Waiter(seconds: 1);
            iss.Variables.Add(new SessionStateVariableEntry("waiter", w, "Waiter test class"));

            double durationSeconds;
            using (var ps = PowerShell.Create(iss))
            {
                ps.AddScript(@"
$timing = measure-command { $waiter.DoWait() }
$timing.TotalSeconds
");
                durationSeconds = ps.Invoke<double>().First();
            }
            Assert.IsTrue(durationSeconds > 1);
        }

        [TestMethod]
        public void TestMethod7()
        {
            Action<IAsyncResult> end = result =>
            {
                TestContext.WriteLine("TestMethod7_end");
                ((AsyncResultNoResult) result).EndInvoke();
            };

            // signature looks like Stream.BeginRead
            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin =
                (bytes, i, j, callback, state) =>
                {
                    TestContext.WriteLine("TestMethod7_begin");
                    var inner = new AsyncResultNoResult(callback, state: null);
                    ThreadPool.QueueUserWorkItem(
                        _ =>
                        {
                            TestContext.WriteLine("TestMethod7_AsyncWork");
                            Thread.Sleep(500);
                            TestContext.WriteLine("TestMethod7_AsyncWork_Complete");
                            inner.SetAsCompleted(null, false);
                        }, state);

                    return inner;
                };

            // invoke our Begin
            IAsyncResult outer = begin.Invoke(
                (new byte[] {42}), 42, 42,
                new AsyncCallback(end.Invoke), null); // asynccallback, state

            // block
            outer.AsyncWaitHandle.WaitOne(1000); // wait for a second
        }

        [TestMethod]
        public void TestMethod8()
        {
            var waiter = new Waiter(1);
            TaskFactory<DateTime> factory = Task<DateTime>.Factory;

            var task = factory.FromAsync(
                waiter.BeginDoWait,
                waiter.EndDoWait,
                42);

            var time = task.Result;
            TestContext.WriteLine("Result is {0}", time);
            Assert.IsTrue(DateTime.Now.Ticks > time.Ticks);
        }

        [TestMethod]
        public void TestMethod9()
        {
            var waiter = new Waiter(1);
            dynamic factory = Task<DateTime>.Factory;

            var task = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWait), // begin
                new Func<IAsyncResult, DateTime>(waiter.EndDoWait), // end
                42); // state

            var time = task.Result;

            TestContext.WriteLine("Result is {0}", time);
            Assert.IsTrue(DateTime.Now.Ticks > time.Ticks);
        }

        [TestMethod]
        public void TestMethod10()
        {
            var waiter = new Waiter(1);
            TaskFactory factory = Task.Factory;

            var task = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                42); // state

            task.Wait();
        }

        [TestMethod]
        public void TestMethod11() // from 9
        {
            var waiter = new Waiter(1);
            object factory = Task<DateTime>.Factory;

            // Create call site for correct generic overload of FromAsync
            var site1a = CallSite
                <
                    Func
                        <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Func<IAsyncResult, DateTime>, int,
                            object>>
                .Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync", null, typeof (UnitTest1),
                    new CSharpArgumentInfo[]
                    {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant |
                                                  CSharpArgumentInfoFlags.UseCompileTimeType, null)
                    }));

            // var task = factory.FromAsync(
            object obj3 = site1a.Target(site1a, factory,
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWait),
                new Func<IAsyncResult, DateTime>(waiter.EndDoWait),
                0x2a);

            var site1b =
                CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, "Result",
                    typeof (UnitTest1),
                    new CSharpArgumentInfo[] {CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)}));

            object obj4 = site1b.Target(site1b, obj3);
            var site1c =
                CallSite<Action<CallSite, TestContext, string, object>>.Create(
                    Binder.InvokeMember(CSharpBinderFlags.ResultDiscarded, "WriteLine", null, typeof (UnitTest1),
                        new CSharpArgumentInfo[]
                        {
                            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                            CSharpArgumentInfo.Create(
                                CSharpArgumentInfoFlags.Constant | CSharpArgumentInfoFlags.UseCompileTimeType, null),
                            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                        }));

            site1c.Target(site1c, this.TestContext, "Result is {0}", obj4);

            var site1d = CallSite<Action<CallSite, Type, object>>.Create(
                Binder.InvokeMember(CSharpBinderFlags.ResultDiscarded, "IsTrue", null, typeof (UnitTest1),
                    new CSharpArgumentInfo[]
                    {
                        CSharpArgumentInfo.Create(
                            CSharpArgumentInfoFlags.IsStaticType | CSharpArgumentInfoFlags.UseCompileTimeType, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                    }));

            var site1e = CallSite<Func<CallSite, long, object, object>>.Create(
                Binder.BinaryOperation(CSharpBinderFlags.None, ExpressionType.GreaterThan, typeof (UnitTest1),
                    new CSharpArgumentInfo[]
                    {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                    }));

            var site1f = CallSite<Func<CallSite, object, object>>.Create(
                Binder.GetMember(CSharpBinderFlags.None, "Ticks", typeof (UnitTest1),
                    new CSharpArgumentInfo[] {CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)}));

            site1d.Target(site1d, typeof (Assert), site1e.Target(site1e, DateTime.Now.Ticks,
                site1f.Target(site1f, obj4)));
        }

        [TestMethod]
        public void TestMethod12() // from 10
        {
            var waiter = new Waiter(1);
            object factory = Task.Factory;

            // dereferenced inputs
            object state = 42;
            var begin = new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult);
            var end = new Action<IAsyncResult>(waiter.EndDoWaitNoResult);

            // type of dereferenced inputs
            Type beginType = typeof (Func<AsyncCallback, object, IAsyncResult>);
            Type endType = typeof (Action<IAsyncResult>);
            Type stateType = state.GetType();

#if USE_COMPILER_CODEGEN
    // COMPILER:
            CallSiteBinder binder = Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync", null,
                typeof (UnitTest1),
                new CSharpArgumentInfo[]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                    CSharpArgumentInfo.Create(
                        CSharpArgumentInfoFlags.Constant |
                        CSharpArgumentInfoFlags.UseCompileTimeType, null)
                });

            // COMPILER:
            var site21 =
                CallSite
                    <Func
                        <CallSite,
                            object,
                            Func<AsyncCallback, object, IAsyncResult>, // begin type
                            Action<IAsyncResult>, // end type
                            int, // typeof state
                            object>>.Create(binder);
#else
            var binderArgs = new List<CSharpArgumentInfo>();
            Debug.Assert(beginType.IsGenericType, "beginType.IsGenericType");

            binderArgs.Add(CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null));
            binderArgs.Add(CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null));
            binderArgs.Add(CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null));

            // handle state

            if (beginType.GetGenericArguments().Length > 3)
            {
                // TODO
            }

            CallSiteBinder binder = Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync", null,
                typeof (UnitTest1),
                new CSharpArgumentInfo[]
                {
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null), // This should never change
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                    // This should never change (always a func)
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
                    // this should never change (always func or action)
                    CSharpArgumentInfo.Create(
                        CSharpArgumentInfoFlags.Constant |
                        CSharpArgumentInfoFlags.UseCompileTimeType, null) // typeof state (constant if int)
                });

            Type callSiteType = typeof (Func<,,,,,>) // TODO: this open type must be found at runtime
                .MakeGenericType(
                    typeof (CallSite),
                    typeof (Object),
                    /*beginType,*/
                    typeof (Func<,,>).MakeGenericType(typeof (AsyncCallback), typeof (Object), typeof (IAsyncResult)),
                    /*endType,*/ typeof (Action<>).MakeGenericType(typeof (IAsyncResult)),
                    /*state*/ typeof (Int32),
                    typeof (Object));
            var createMethodInfo = callSiteType.GetMethod("Create", new Type[] {typeof (CallSiteBinder)});
            object site21 = createMethodInfo.Invoke(callSiteType, new object[] {binder});
#endif

#if USE_COMPILER_CODEGEN
    // COMPILER: invoke factory.FromAsync(...) returning Task
            object obj3 = site21.Target(site21, factory,
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), 0x2a); // << state
#else
            var targetMethodInfoObj3 = site21.GetType()
                .GetMethod("Target",
                    new Type[]
                    {
                        site21.GetType(), factory.GetType(), beginType, endType, stateType
                    });
            object obj3 = targetMethodInfoObj3.Invoke(site21, new object[]
            {
                site21, factory, begin, end, state
            });
#endif

#if USE_COMPILER_CODEGEN
    // COMPILER: bind to Task.Wait
            var site22 =
                CallSite<Action<CallSite, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.ResultDiscarded, "Wait",
                    null, typeof (UnitTest1),
                    new CSharpArgumentInfo[] {CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)}));

            // COMPILER: invoke task.Wait()
            site22.Target(site22, obj3);        
#else

#endif
        }

        [TestMethod]
        public void TestMethod13()
        {
            var emitter = Emit<Action<Task>>.NewDynamicMethod(typeof (DynamicTypes));
        }

        [TestMethod]
        public void TestMethod14()
        {
            var waiter = new Waiter(1);
            dynamic factory = Task.Factory;

            var stateInt = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                42); // state

            var stateDouble = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                42d); // state

            var foo = new FooBar(1, 2);
            var stateValue = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                foo); // state

            var dtype = new DynamicTypes();
            var stateClass = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                dtype); // state

            var str = "Hello";
            var stateString = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                str); // state

            object nullRef = null;
            var stateNullRef = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                nullRef); // state

            var stateNullLit = factory.FromAsync(
                new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult), // begin
                new Action<IAsyncResult>(waiter.EndDoWaitNoResult), // end
                null); // state
        }

        //[TestMethod]
        public void TestMethod14_decompiled()
        {
            //Waiter waiter = new Waiter(1);
            //object factory = Task.Factory;
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site21 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site21 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>, int,
            //                        object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync", null,
            //                            typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(
            //                                    CSharpArgumentInfoFlags.Constant |
            //                                    CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //}
            //object obj3 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site21.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site21,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //0x2a)
            //;
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site22 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site22 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        double, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync",
            //                            null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(
            //                                    CSharpArgumentInfoFlags.Constant |
            //                                    CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //}
            //object obj4 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site22.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site22,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //42.0)
            //;
            //FooBar bar = new FooBar(1, 2);
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site23 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site23 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        FooBar, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync",
            //                            null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //}
            //object obj5 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site23.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site23,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //bar)
            //;
            //DynamicTypes types = new DynamicTypes();
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site24 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site24 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        DynamicTypes, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None,
            //                            "FromAsync", null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //}
            //object obj6 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site24.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site24,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //types)
            //;
            //string str = "Hello";
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site25 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site25 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        string, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync",
            //                            null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //        // string
            //}
            //object obj7 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site25.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site25,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //str)
            //;
            //object obj8 = null;
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site26 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site26 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        object, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync",
            //                            null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null)
            //                            }));
            //        // null reference (object)
            //}
            //object obj9 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site26.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site26,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //obj8)
            //;
            //if (<
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site27 == null)
            //{
            //<
            //    TestMethod14 > o__SiteContainer20.<>
            //    p__Site27 =
            //        CallSite
            //            <
            //                Func
            //                    <CallSite, object, Func<AsyncCallback, object, IAsyncResult>, Action<IAsyncResult>,
            //                        object, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "FromAsync",
            //                            null, typeof (UnitTest1),
            //                            new CSharpArgumentInfo[]
            //                            {
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType, null),
            //                                CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant, null)
            //                            }));
            //        // null literal
            //}
            //object obj10 = <
            //TestMethod14 > o__SiteContainer20.<>
            //p__Site27.Target( < TestMethod14 > o__SiteContainer20.<>
            //p__Site27,
            //factory,
            //new Func<AsyncCallback, object, IAsyncResult>(waiter.BeginDoWaitNoResult),
            //new Action<IAsyncResult>(waiter.EndDoWaitNoResult),
            //null)
            //;
        }
    }
}

internal struct FooBar
    {
        public int X { get; set; }
        public int Y { get; set; }

        internal FooBar(int x, int y) : this()
        {
            X = x;
            Y = y;
        }
    }

    internal class DynamicTypes
    {
    }

    internal class TaskBuilder
    {
        private Type _beginType;
        private Delegate _beginHandler;
        private Type _endType;
        private Delegate _endHandler;

        public TaskBuilder SetBegin([NotNull] Delegate begin)
        {
            if (begin == null) throw new ArgumentNullException("begin");
            
            _beginHandler = begin;
            _beginType = begin.GetType();

            return this;
        }

        public TaskBuilder SetEnd([NotNull] Delegate end)
        {
            if (end == null) throw new ArgumentNullException("end");
            
            _endHandler = end;
            _endType = end.GetType();
            
            return this;
        }
        
        public Task ToTask(params object[] arguments)
        {
            if (_beginHandler == null || _endHandler == null)
            {
                throw new InvalidOperationException("SetBegin and/or EndBegin must be used to assign handlers.");
            }
            return null;
        }

        private Type GetDelegateType(params Type[] typeArguments)
        {
            Type type = null;
            switch (typeArguments.Length)
            {
                case 3:
                    type = typeof(Func<,,,,,>);
                    break;
                case 2:
                    type = typeof(Func<,,,,>);
                    break;
                case 1:
                    type = typeof(Func<,,,>);
                    break;
                case 0:
                    type = typeof(Func<,,>);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("typeArguments",
                        "Too many arguments for Begin handler!");
            }
            return type.MakeGenericType(typeArguments);
        }
    }


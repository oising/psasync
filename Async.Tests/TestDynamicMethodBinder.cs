using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nivot.PowerShell.Async;

namespace Async.Tests
{
    public class Dummy
    {
        public static void SimpleAction(Action action)
        {
            action();
        }

        public static T SimpleFunc<T>(Func<T> func)
        {
            return func();
        }

        public void GenericA<T>(Action<T> action, T arg)
        {
            action(arg);
        }

        public TResult GenericA<T1, TResult>(Func<T1, TResult> func, T1 arg1)
        {
            return func(arg1);
        }

        public TResult GenericA<T1, T2, TResult>(Func<T1, T2, TResult> func, T1 arg1, T2 arg2)
        {
            return func(arg1, arg2);
        }

        public static void GenericB<T>(Action<T> action, T arg)
        {
            action(arg);
        }

        public static TResult GenericB<T1, TResult>(Func<T1, TResult> func, T1 arg1)
        {
            return func(arg1);
        }

        public static TResult GenericB<T1, T2, TResult>(Func<T1, T2, TResult> func, T1 arg1, T2 arg2)
        {
            return func(arg1, arg2);
        }
    }

    [TestClass]
    public class TestDynamicMethodBinder
    {

        [TestMethod]
        public void SimpleInstanceInvoke()
        {
            var builder = new StringBuilder("Hello, ");            
            var result = DynamicMethodBinder.Invoke<object>(builder, "Append", false, "World.");
            Assert.IsInstanceOfType(result, typeof(StringBuilder));
            Assert.IsTrue(result.ToString() == "Hello, World.");
        }

        [TestMethod]
        public void SimpleInstanceDiscardedInvoke()
        {
            var val = 0;
            Func<int> func = () => val = 42;
            DynamicMethodBinder.InvokeNoResult(func, "Invoke", false);
            Assert.IsTrue(val == 42);
        }
        [TestMethod]
        public void SimpleInstanceVoidInvoke()
        {
            var val = 0;
            Action action = () => val = 42;
            DynamicMethodBinder.InvokeNoResult(action, "Invoke", false);
            Assert.IsTrue(val == 42);
        }

        [TestMethod]
        public void SimpleStaticInvoke()
        {
            string result = DynamicMethodBinder.Invoke<string>(
                typeof (String), "Format", true, "Hello, {0}.", "World");
            Assert.IsTrue(result == "Hello, World.");
        }

        [TestMethod]
        public void SimpleStaticDiscardedInvoke()
        {
            DynamicMethodBinder.InvokeNoResult(
                typeof(String), "Format", true, "Hello, {0}.", "World");
        }

        [TestMethod]
        public void SimpleStaticVoidInvoke()
        {
            int value = 0;
            Action action = () => value = 42;
            DynamicMethodBinder.InvokeNoResult(typeof(Dummy), "SimpleAction", true, action);
            Assert.IsTrue(value == 42);
        }

        [TestMethod]
        public void GenericInstanceInvoke()
        {
            var d = new Dummy();
            int value = 1;
            Func<int, int> func2 = incr => value + incr;
            var result = DynamicMethodBinder.Invoke<int>(d, "GenericA", false, func2, 41);
            Assert.IsTrue(result == 42);

            // try different overload
            Func<int, int, int> func3 = (incr1, incr2) => value + incr1 + incr2;
            result = DynamicMethodBinder.Invoke<int>(d, "GenericA", false, func3, 20, 21);
            Assert.IsTrue(result == 42);
        }

        [TestMethod]
        public void GenericInstanceDiscardedInvoke()
        {
            var d = new Dummy();
            int value = 1;
            Func<int, int> func = incr => value = value + incr;
            DynamicMethodBinder.InvokeNoResult(d, "GenericA", false, func, 41);
            Assert.IsTrue(value == 42);
        }

        [TestMethod]
        public void GenericInstanceVoidInvoke()
        {
            var d = new Dummy();
            int value = 1;
            Action<int> action = incr => value = value + incr;
            DynamicMethodBinder.InvokeNoResult(d, "GenericA", false, action, 41);
            Assert.IsTrue(value == 42);
        }

        [TestMethod]
        public void GenericStaticInvoke()
        {
            int value = 1;
            Func<int, int> func2 = incr => value + incr;
            var result = DynamicMethodBinder.Invoke<int>(typeof(Dummy), "GenericB", true, func2, 41);
            Assert.IsTrue(result == 42);

            // try different overload
            Func<int, int, int> func3 = (incr1, incr2) => value + incr1 + incr2;
            result = DynamicMethodBinder.Invoke<int>(typeof(Dummy), "GenericB", true, func3, 20, 21);
            Assert.IsTrue(result == 42);
        }

        [TestMethod]
        public void GenericStaticDiscardedInvoke()
        {
            int value = 1;
            Func<int, int> func2 = incr => value = value + incr;
            DynamicMethodBinder.InvokeNoResult(typeof(Dummy), "GenericB", true, func2, 41);
            Assert.IsTrue(value == 42);
        }

        [TestMethod]
        public void GenericStaticVoidInvoke()
        {            
            int value = 1;
            Action<int> action = incr => value = value + incr;
            DynamicMethodBinder.InvokeNoResult(typeof(Dummy), "GenericB", true, action, 41);
            Assert.IsTrue(value == 42);
        }
    }
}

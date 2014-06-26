using System;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Nivot.PowerShell.Async;

namespace Async.Tests
{
    [TestClass]
    public class UnitTest1
    {
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

            var data = new byte[] { 1, 2, 3, 4, 5 };

            Expression<Func<IAsyncResult, Task<dynamic>>> expr =
                result => Task<dynamic>.Factory.FromAsync(begin, end, data, 0, 100, null);

            Task<dynamic> meaning = expr.Compile()(null);

        }

        public void TestMethod3()
        {
            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin = (bytes, i, arg3, arg4, arg5) => null;
            Func<IAsyncResult, dynamic> end = result => 42;
            var data = new byte[] { 1, 2, 3, 4, 5 };

            dynamic factory = Task<dynamic>.Factory;
            var task = factory.FromAsync(begin, end, data, 0, 100, null);
        }

        public void TestMethod4()
        {
            Func<byte[], int, int, AsyncCallback, object, IAsyncResult> begin = (bytes, i, arg3, arg4, arg5) => null;
            Func<IAsyncResult, StringBuilder> end = result => new StringBuilder();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            dynamic factory = Task<StringBuilder>.Factory;
            var task = factory.FromAsync(begin, end, data, 0, 100, null);
        }
    }
}

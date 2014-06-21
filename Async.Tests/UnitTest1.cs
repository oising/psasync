using System;
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
    }
}

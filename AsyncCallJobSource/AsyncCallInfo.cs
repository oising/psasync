//#define SUPPORTS_WINRT

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Storage;

using Microsoft.CSharp.RuntimeBinder;

namespace Nivot.PowerShell.Async
{
    public static class AsyncTest
    {

#if SUPPORTS_WINRT
        public async static Task<StorageFile> GetFileAsync(string path)
        {
            // [Windows.Storage.StorageFile]::GetFileFromPathAsync($path)
            return await StorageFile.GetFileFromPathAsync(path);
        }
#endif
        public static IAsyncOperation<StorageFile> GetFileAsyncWinRT(string path)
        {
            return StorageFile.GetFileFromPathAsync(path);
        }

        public static IAsyncResult BeginGetFileAsync(string path, AsyncCallback callback, object state)
        {
            return null;
        }

        public static StorageFile EndGetFileAsync(IAsyncResult result)
        {
            return null;
        }

        public static async Task<StorageFile> TestBeginEndPairingTyped(string path)
        {
            return await Task<StorageFile>.Factory.FromAsync(BeginGetFileAsync, EndGetFileAsync, path, null);
        }

        public static async Task<dynamic> TestBeginEndPairingDynamic(string path)
        {
            return await Task<dynamic>.Factory.FromAsync(BeginGetFileAsync, EndGetFileAsync, path, null);
        }

        public static async Task<dynamic> GetBeginEndPairAsAwaitable<T1>(T1 arg1,
            Func<T1, AsyncCallback, object, IAsyncResult> begin,
            Func<IAsyncResult, dynamic> end)
        {
            return await Task<dynamic>.Factory.FromAsync(begin, end, arg1, null);
        }

        public static void Test2(string path, out dynamic result)
        {
            var task = GetBeginEndPairAsAwaitable(path, BeginGetFileAsync, EndGetFileAsync);
            task.Wait();
            if (task.IsCompleted)
            {
                result = task.Result;
            }
            if (task.IsFaulted)
            {
                Debug.Assert(task.Exception != null, "task.Exception != null");
                throw task.Exception;
            }
            throw new TaskCanceledException(task);
        }

        public static void Test3(string path)
        {
            //var file = new FileStream(Path.GetTempFileName(), FileMode.CreateNew);

            //var begin = new Func<byte[], int, int, AsyncCallback, object, IAsyncResult>(file.BeginWrite);
            //var end = new Action<IAsyncResult>(file.EndWrite);

            //var binding = Binder.InvokeMember(
            //    CSharpBinderFlags.None,
            //    "FromAsync",
            //    new[] { typeof(byte[]), typeof(int), typeof(int)},
            //    null,
            //    new[]
            //        {
            //            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.Constant|CSharpArgumentInfoFlags.UseCompileTimeType, null),

            //        }
        }
    }

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
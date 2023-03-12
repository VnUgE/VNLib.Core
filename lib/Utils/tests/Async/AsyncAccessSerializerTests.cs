
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VNLib.Utils.Async.Tests
{
    [TestClass()]
    public class AsyncAccessSerializerTests
    {
        [TestMethod()]
        public void AsyncAccessSerializerTest()
        {
            /*
             * Very basic single threaded test to confrim
             * async serialzation of a given resource
             */

            const string DEFAULT_KEY = "default";

            //Alloc serailzer base on string 
            IAsyncAccessSerializer<string> serializer = new AsyncAccessSerializer<string>(100, 100, StringComparer.Ordinal);

            Task first = serializer.WaitAsync(DEFAULT_KEY);

            //The first call to wait should complete synchronously
            Assert.IsTrue(first.IsCompleted);

            //Second call to wait should yeild async
            Task second = serializer.WaitAsync(DEFAULT_KEY);
            Assert.IsFalse(second.IsCompleted);

            //Create a 3rd call to wait
            Task third = serializer.WaitAsync(DEFAULT_KEY);
            Assert.IsFalse(third.IsCompleted);

            //Release one call
            serializer.Release(DEFAULT_KEY);

            //Second call can be called sync
            second.GetAwaiter().GetResult();

            //Third call should still be waiting
            Assert.IsFalse(third.IsCompleted);

            //Second release
            serializer.Release(DEFAULT_KEY);
            third.GetAwaiter().GetResult();

            //Third/final release
            serializer.Release(DEFAULT_KEY);

            //Confirm an excess release raises exception
            Assert.ThrowsException<KeyNotFoundException>(() => serializer.Release(DEFAULT_KEY));
        }

        /*
         * Tests the async cancellation feature of the async
         * wait.
         */
        [TestMethod()]
        public void AsyncAccessSerializerCancellationTest()
        {
            const string DEFAULT_KEY = "default";

            //Alloc serailzer base on string 
            IAsyncAccessSerializer<string> serializer = new AsyncAccessSerializer<string>(100, 100, StringComparer.Ordinal);

            //Enter the wait one time and dont release it
            _ = serializer.WaitAsync(DEFAULT_KEY);

            using CancellationTokenSource cts = new();

            //try to enter again
            Task reentry = serializer.WaitAsync(DEFAULT_KEY, cts.Token);

            //confirm an async await is requested
            Assert.IsFalse(reentry.IsCompleted);

            //Cancel the cts and confirm cancelled result
            cts.Cancel();

            //Confirm the task raises cancellation
            Assert.ThrowsException<OperationCanceledException>(() => reentry.GetAwaiter().GetResult());
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
            Assert.ThrowsException<TaskCanceledException>(() => reentry.GetAwaiter().GetResult());
        }

        [TestMethod()]
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MultiThreadedAASTest()
        {
            const string DEFAULT_KEY = "default";

            //Alloc serailzer base on string 
            IAsyncAccessSerializer<string> serializer = new AsyncAccessSerializer<string>(100, 100, StringComparer.Ordinal);
            
            int maxCount = 64;

            Task[] asyncArr = new int[maxCount].Select(p => Task.Run(async () =>
            {
                 //Take a lock then random delay, then release
                Task entry = serializer.WaitAsync(DEFAULT_KEY);

                bool isCompleted = entry.IsCompleted;

                Trace.WriteLineIf(isCompleted, "Wait was entered synchronously");

                await Task.Delay(Random.Shared.Next(0, 10));

                Trace.WriteLineIf(isCompleted != entry.IsCompleted, "Wait has transitioned to completed while waiting");
                Trace.WriteLineIf(!entry.IsCompleted, "A call to wait will yield");

                await entry;

                serializer.Release(DEFAULT_KEY);

            })).ToArray();

            Task.WaitAll(asyncArr);
        }

        [TestMethod()]
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void RaceProtectionAASTest()
        {
            /*
             * A very basic critical section to confirm threading consistency.
             * 
             * Mutuating a string should be a reasonably large number of instructions due 
             * to the allocation copy, and assignment, to cause a race condition.
             * 
             * Testing to make sure the string is updated consitently during a multi threaded 
             * process, and that a race condition occured when not using the serializer is a 
             * non-ideal test, but it is a simple test to confirm the serializer is working.
             */

            const string DEFAULT_KEY = "default";

            //Alloc serailzer base on string 
            IAsyncAccessSerializer<string> serializer = new AsyncAccessSerializer<string>(100, 100, StringComparer.Ordinal);

            int maxCount = 128;
            string serialized = "";

            using CancellationTokenSource cts = new(500);

            Task[] asyncArr = new int[maxCount].Select(p => Task.Run(async () =>
            {
                //Take a lock then random delay, then release
                await serializer.WaitAsync(DEFAULT_KEY, cts.Token);

                //Increment count 
                serialized += "0";

                serializer.Release(DEFAULT_KEY);

            })).ToArray();

            Task.WaitAll(asyncArr);

            //Make sure count did not encounter any race conditions
            Assert.AreEqual(maxCount, serialized.Length);
        }

        [TestMethod()]
        public void SimplePerformanceComparisonTest()
        {
            const string DEFAULT_KEY = "default";

            //Alloc serailzer base on string 
            IAsyncAccessSerializer<string> serializer = new AsyncAccessSerializer<string>(100, 100, StringComparer.Ordinal);

            const int maxCount = 128;
            const int itterations = 20;
            string test = "";
            Stopwatch timer = new();

            using CancellationTokenSource cts = new(500);

            for (int i = 0; i < itterations; i++)
            {
                test = "";
                timer.Restart();              

                Task[] asyncArr = new int[maxCount].Select(p => Task.Run(async () =>
                {
                    //Take a lock then random delay, then release
                    await serializer.WaitAsync(DEFAULT_KEY, cts.Token);

                    //Increment count 
                    test += "0";

                    serializer.Release(DEFAULT_KEY);

                })).ToArray();

                Task.WaitAll(asyncArr);

                timer.Stop();

                Trace.WriteLine($"Async serialzier test completed in {timer.ElapsedTicks / 10} microseconds");
                Assert.AreEqual(maxCount, test.Length);
            }          

            using SemaphoreSlim slim = new(1,1);

            for (int i = 0; i < itterations; i++)
            {
                test = "";
                timer.Restart();

                Task[] asyncArr = new int[maxCount].Select(p => Task.Run(async () =>
                {
                    //Take a lock then random delay, then release
                    await slim.WaitAsync(cts.Token);

                    //Increment count 
                    test += "0";

                    slim.Release();
                })).ToArray();

                Task.WaitAll(asyncArr);

                timer.Stop();

                Trace.WriteLine($"SemaphoreSlim test completed in {timer.ElapsedTicks / 10} microseconds");

                Assert.AreEqual(maxCount, test.Length);
            }
        }
    }
}
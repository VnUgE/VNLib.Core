/*
* Copyright (c) 2026 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.UtilsTests
* File: AsyncLazyTests.cs 
*
* AsyncLazyTests.cs is part of VNLib.UtilsTests which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.UtilsTests is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.UtilsTests is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.UtilsTests. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Extensions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Utils.Async.Tests
{
    [TestClass()]
    public class AsyncLazyTests
    {
        /// <summary>
        /// Verifies that AsLazy on a completed task returns a non-null 
        /// lazy with Completed set to true.
        /// </summary>
        [TestMethod()]
        public void AsLazy_CompletedTask_ReturnsNonNullWithCompletedTrue()
        {
            Task<int> completedTask = Task.FromResult(42);
            IAsyncLazy<int> lazy = completedTask.AsLazy();

            Assert.IsNotNull(lazy);
            Assert.IsTrue(lazy.Completed);
        }

        /// <summary>
        /// Verifies that AsLazy on an incomplete task reports 
        /// Completed as false.
        /// </summary>
        [TestMethod()]
        public void AsLazy_IncompleteTask_CompletedIsFalse()
        {
            TaskCompletionSource<int> tcs = new();
            IAsyncLazy<int> lazy = tcs.Task.AsLazy();

            Assert.IsFalse(lazy.Completed);
        }

        /// <summary>
        /// Verifies that AsLazy throws ArgumentNullException when 
        /// the source task is null.
        /// </summary>
        [TestMethod()]
        public void AsLazy_NullTask_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => ((Task<int>)null!).AsLazy());
        }

        /// <summary>
        /// Verifies that awaiting a completed lazy returns the 
        /// task result.
        /// </summary>
        [TestMethod()]
        public async Task Await_CompletedLazy_ReturnsResult()
        {
            IAsyncLazy<int> lazy = Task.FromResult(42).AsLazy();

            int result = await lazy;

            Assert.AreEqual(42, result);
        }

        /// <summary>
        /// Verifies that awaiting a faulted lazy propagates the 
        /// stored exception.
        /// </summary>
        [TestMethod()]
        public async Task Await_FaultedLazy_PropagatesException()
        {
            IAsyncLazy<int> lazy = Task.FromException<int>(new InvalidOperationException("boom")).AsLazy();

            InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(lazy.AsTask);

            Assert.AreEqual("boom", ex.Message);
        }

        /// <summary>
        /// Verifies that awaiting a canceled lazy propagates a 
        /// TaskCanceledException.
        /// </summary>
        [TestMethod()]
        public async Task Await_CanceledLazy_PropagatesCancellation()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            IAsyncLazy<int> lazy = Task.FromCanceled<int>(cts.Token).AsLazy();

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(lazy.AsTask);
        }

        /// <summary>
        /// Verifies that accessing Value on an incomplete lazy throws 
        /// InvalidOperationException.
        /// </summary>
        [TestMethod()]
        public void Value_IncompleteTask_ThrowsInvalidOperationException()
        {
            TaskCompletionSource<int> tcs = new();
            IAsyncLazy<int> lazy = tcs.Task.AsLazy();

            Assert.ThrowsExactly<InvalidOperationException>(() => _ = lazy.Value);
        }

        /// <summary>
        /// Verifies that accessing Value on a completed lazy returns 
        /// the task result.
        /// </summary>
        [TestMethod()]
        public void Value_CompletedTask_ReturnsResult()
        {
            IAsyncLazy<int> lazy = Task.FromResult(99).AsLazy();

            Assert.AreEqual(99, lazy.Value);
        }

        /// <summary>
        /// Verifies that accessing Value on a faulted lazy throws the 
        /// stored exception.
        /// </summary>
        [TestMethod()]
        public void Value_FaultedTask_ThrowsStoredException()
        {
            IAsyncLazy<int> lazy = Task.FromException<int>(new DivideByZeroException()).AsLazy();

            Assert.ThrowsExactly<DivideByZeroException>(() => _ = lazy.Value);
        }

        /// <summary>
        /// Verifies that accessing Value on a canceled lazy throws 
        /// TaskCanceledException.
        /// </summary>
        [TestMethod()]
        public void Value_CanceledTask_ThrowsTaskCanceledException()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            IAsyncLazy<int> lazy = Task.FromCanceled<int>(cts.Token).AsLazy();

            Assert.ThrowsExactly<TaskCanceledException>(() => _ = lazy.Value);
        }

        /// <summary>
        /// Verifies that repeated calls to Value return the same 
        /// cached result.
        /// </summary>
        [TestMethod()]
        public void Value_RepeatedCalls_ReturnCachedResult()
        {
            IAsyncLazy<int> lazy = Task.FromResult(7).AsLazy();

            Assert.AreEqual(7, lazy.Value);
            Assert.AreEqual(7, lazy.Value);
            Assert.AreEqual(7, lazy.Value);
        }

        /// <summary>
        /// Verifies that AsTask returns the same underlying task 
        /// instance by reference equality.
        /// </summary>
        [TestMethod()]
        public void AsTask_ReturnsUnderlyingTaskInstance()
        {
            Task<int> task = Task.FromResult(123);
            IAsyncLazy<int> lazy = task.AsLazy();

            Assert.AreSame(task, lazy.AsTask());
        }

        /// <summary>
        /// Verifies that Transform throws ArgumentNullException when 
        /// the lazy source is null.
        /// </summary>
        [TestMethod()]
        public void Transform_NullLazy_ThrowsArgumentNullException()
        {
            IAsyncLazy<int>? lazy = null;

            Assert.ThrowsExactly<ArgumentNullException>(() => lazy!.Transform(_ => "x"));
        }

        /// <summary>
        /// Verifies that Transform throws ArgumentNullException when 
        /// the handler function is null.
        /// </summary>
        [TestMethod()]
        public void Transform_NullHandler_ThrowsArgumentNullException()
        {
            IAsyncLazy<int> lazy = Task.FromResult(1).AsLazy();

            Assert.ThrowsExactly<ArgumentNullException>(() => lazy.Transform<int, string>(null!));
        }

        /// <summary>
        /// Verifies that awaiting a transformed lazy produces the 
        /// transformed result.
        /// </summary>
        [TestMethod()]
        public async Task Transform_CompletedLazy_ProducesTransformedResult()
        {
            IAsyncLazy<int> lazy = Task.FromResult(10).AsLazy();
            IAsyncLazy<string> transformed = lazy.Transform(x => x.ToString());

            Assert.AreEqual("10", await transformed);
        }

        /// <summary>
        /// Verifies that awaiting a transformed lazy propagates the 
        /// source exception when the source task is faulted.
        /// </summary>
        [TestMethod()]
        public async Task Transform_FaultedLazy_PropagatesSourceException()
        {
            IAsyncLazy<int> lazy = Task.FromException<int>(new InvalidOperationException("source fail")).AsLazy();
            IAsyncLazy<string> transformed = lazy.Transform(x => x.ToString());

            InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(transformed.AsTask);

            Assert.AreEqual("source fail", ex.Message);
        }
    }
}

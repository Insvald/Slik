using DotNext.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class NamedLockFactoryTests
    {
        private static async Task<Tuple<bool, bool>> ExecuteTwoTasksWithLocks(
            Func<NamedLockFactory, Task<AsyncLock.Holder>> firstLockFunction, 
            Func<NamedLockFactory, Task<AsyncLock.Holder>> secondLockFunction)
        {
            bool firstTaskEntered = false;
            bool secondTaskEntered = false;
            var cts = new CancellationTokenSource();

            await using var lockFactory = new NamedLockFactory();

            var firstTask = Task.Run(async () =>
            {
                using (await firstLockFunction(lockFactory))
                {
                    firstTaskEntered = true;

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.3), cts.Token);
                    }
                    catch (TaskCanceledException) { }
                };
            });

            // to be sure that the task had time to start
            await Task.Delay(50);

            var secondTask = Task.Run(async () =>
            {
                using (await secondLockFunction(lockFactory))
                {
                    secondTaskEntered = true;

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.3), cts.Token);
                    }
                    catch (TaskCanceledException) { }

                };
            });

            // to be sure that all tasks had time to start
            await Task.Delay(50);

            var result = new Tuple<bool, bool>(firstTaskEntered, secondTaskEntered);

            cts.Cancel();

            Task.WaitAll(firstTask, secondTask);

            return result;
        }

        [TestMethod]
        public async Task AcquireWriteLockAsync_SameName_BlocksWrites()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireWriteLockAsync("key"), 
                lockFactory => lockFactory.AcquireWriteLockAsync("key"));

            Assert.IsTrue(result.Item1);
            Assert.IsFalse(result.Item2);
        }

        [TestMethod]
        public async Task AcquireWriteLockAsync_DifferentName_AllowsWrites()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireWriteLockAsync("key1"), 
                lockFactory => lockFactory.AcquireWriteLockAsync("key2"));

            Assert.IsTrue(result.Item1);
            Assert.IsTrue(result.Item2);
        }

        [TestMethod]
        public async Task AcquireReadLockAsync_SameName_AllowsReads()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireReadLockAsync("key"),
                lockFactory => lockFactory.AcquireReadLockAsync("key"));

            Assert.IsTrue(result.Item1);
            Assert.IsTrue(result.Item2);
        }

        [TestMethod]
        public async Task AcquireReadLockAsync_DifferentName_AllowsReads()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireReadLockAsync("key1"),
                lockFactory => lockFactory.AcquireReadLockAsync("key2"));

            Assert.IsTrue(result.Item1);
            Assert.IsTrue(result.Item2);
        }

        [TestMethod]
        public async Task AcquireWriteLockAsync_SameName_BlocksReads()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireWriteLockAsync("key"),
                lockFactory => lockFactory.AcquireReadLockAsync("key"));

            Assert.IsTrue(result.Item1);
            Assert.IsFalse(result.Item2);
        }

        [TestMethod]
        public async Task AcquireReadLockAsync_SameName_BlocksWrites()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireReadLockAsync("key"),
                lockFactory => lockFactory.AcquireWriteLockAsync("key"));

            Assert.IsTrue(result.Item1);
            Assert.IsFalse(result.Item2);
        }

        [TestMethod]
        public async Task AcquireWriteLockAsync_DifferentName_AllowsReads()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireWriteLockAsync("key1"),
                lockFactory => lockFactory.AcquireReadLockAsync("key2"));

            Assert.IsTrue(result.Item1);
            Assert.IsTrue(result.Item2);
        }

        [TestMethod]
        public async Task AcquireReadLockAsync_DifferentName_AllowsWrites()
        {
            var result = await ExecuteTwoTasksWithLocks(
                lockFactory => lockFactory.AcquireReadLockAsync("key1"),
                lockFactory => lockFactory.AcquireWriteLockAsync("key2"));

            Assert.IsTrue(result.Item1);
            Assert.IsTrue(result.Item2);
        }
    }
}

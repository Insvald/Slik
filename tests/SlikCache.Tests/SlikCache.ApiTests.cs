using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class SlikCacheApiTests
    {
        private readonly SlikCache _cache = SlikCacheHelper.InitCache();       

        public class MissingKeyException : Exception
        {
            public MissingKeyException() : base("No such key in the cache") { }
        }

        private async Task<byte[]> GetKeyOrThrowAsync(string key) 
            => await _cache.GetAsync(key) ?? throw new MissingKeyException();

        [TestCleanup]
        public void TestCleanup()
        {
            SlikCacheHelper.DestroyCache(_cache);
        }

        [TestMethod]
        public async Task GetAsync_NonExistentKey_ReturnsNull()
        {
            var value = await _cache.GetAsync("key1");
            Assert.IsNull(value);
        }

        [TestMethod]
        public async Task GetSetAsync_AValue_SavesAndRetirevesTheValue()
        {
            byte[] expectedSequence = new byte[] { 1, 2, 3 };
            _cache.Set("key1", expectedSequence, new DistributedCacheEntryOptions { });
            var actualSequence = await GetKeyOrThrowAsync("key1");

            Assert.IsTrue(expectedSequence.SequenceEqual(actualSequence));
        }

        [TestMethod]
        public async Task SetAsync_AValue_OverwritesPreviousValue()
        {
            byte[] expectedSequence = new byte[] { 1, 2, 3 };
            await _cache.SetAsync("key1", new byte[] { 0 }, new DistributedCacheEntryOptions { });
            await _cache.SetAsync("key1", expectedSequence, new DistributedCacheEntryOptions { });
            var actualSequence = await GetKeyOrThrowAsync("key1");

            Assert.IsTrue(expectedSequence.SequenceEqual(actualSequence));
        }

        [TestMethod]
        public async Task RemoveAsync_ExistingValue_ClearsIt()
        {
            string key = "key1";
            await _cache.SetAsync(key, new byte[] { 0 }, new DistributedCacheEntryOptions { });
            await _cache.RemoveAsync(key);
            var actualValue = await _cache.GetAsync(key);

            Assert.IsNull(actualValue);
        }

        [TestMethod]
        public async Task RemoveAsync_NonExistingValue_DoesNotThrowException()
        {
            await _cache.RemoveAsync("key1");
        }

        private async Task SetAsync_Expiration_ExpiresInTime(Func<TimeSpan, DistributedCacheEntryOptions> getOptions)
        {
            int timeoutInMs = 100;
            TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutInMs);
            byte[] expectedSequence = new byte[] { 1, 2, 3 };
            string key = "key1";

            await _cache.SetAsync(key, expectedSequence, getOptions(timeout));
            var actualSequence = await GetKeyOrThrowAsync(key);
            Assert.IsTrue(expectedSequence.SequenceEqual(actualSequence));

            await Task.Delay(TimeSpan.FromMilliseconds(timeoutInMs * 1.2)); // wait 20% more to be sure
            actualSequence = await _cache.GetAsync(key);

            Assert.IsNull(actualSequence);
        }

        [TestMethod]
        public async Task SetAsync_SlidingExpiration_ExpiresInTime()
        {            
            await SetAsync_Expiration_ExpiresInTime(t => new DistributedCacheEntryOptions { SlidingExpiration = t });
        }

        [TestMethod]
        public async Task SetAsync_AbsoluteExpiration_ExpiresInTime()
        {
            await SetAsync_Expiration_ExpiresInTime(t => new DistributedCacheEntryOptions { AbsoluteExpiration = DateTime.Now + t });
        }

        [TestMethod]
        public async Task SetAsync_AbsoluteExpirationRelativeToNow_ExpiresInTime()
        {
            await SetAsync_Expiration_ExpiresInTime(t => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = t });
        }

        [TestMethod]
        public async Task GetAsync_SlidingExpiration_DoesNotExpire()
        {
            int timeoutInMs = 100;
            byte[] expectedSequence = new byte[] { 1, 2, 3 };
            string key = "key1";

            await _cache.SetAsync(key, expectedSequence, 
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMilliseconds(timeoutInMs) });

            for (int i = 0; i < 10; i++)
            {
                var actualSequence = await GetKeyOrThrowAsync(key);
                Assert.IsTrue(expectedSequence.SequenceEqual(actualSequence));
                await Task.Delay(timeoutInMs / 2);
            }
        }

        [TestMethod]
        public async Task RefreshAsync_SlidingExpiration_DoesNotExpire()
        {
            int timeoutInMs = 100;
            byte[] expectedSequence = new byte[] { 1, 2, 3 };
            string key = "key1";

            await _cache.SetAsync(key, expectedSequence,
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMilliseconds(timeoutInMs) });

            for (int i = 0; i < 10; i++)
            {                                
                await Task.Delay(timeoutInMs / 2);
                await _cache.RefreshAsync(key);
            }

            var actualSequence = await GetKeyOrThrowAsync(key);
            Assert.IsTrue(expectedSequence.SequenceEqual(actualSequence));
        }
    }
}

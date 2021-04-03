using DotNext.IO.Log;
using DotNext.Net.Cluster.Consensus.Raft;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class SlikCachePersistentStateTests
    {
        [TestMethod]
        public async Task InitializeAsync_ExistingLog_AddsItemsCorrectly()
        {
            const string key1 = "key1";
            byte[] expectedValue1 = new byte[] { 3 };
            const string key2 = "key2";

            var records = new CacheLogRecord[]
            {
                new CacheLogRecord(CacheOperation.Update, key1, new byte[] { 1 }),
                new CacheLogRecord(CacheOperation.Update, key2, new byte[] { 2 }),
                new CacheLogRecord(CacheOperation.Update, key1, expectedValue1),
                new CacheLogRecord(CacheOperation.Remove, key2, null),
            };

            string logLocation = string.Empty;

            try
            {
                using (var cache = SlikCacheHelper.InitCache())
                {
                    logLocation = cache.LogLocation;
                    var entries = records.Select(r => (IRaftLogEntry)cache.CreateJsonLogEntry(r)).ToList();

                    await cache.AppendAsync(new LogEntryProducer<IRaftLogEntry>(entries));
                    await cache.CommitAsync(CancellationToken.None);
                }

                //re-creating the cache from the same path
                using (var cache = SlikCacheHelper.InitCache(logLocation))
                {
                    await cache.InitializeAsync();
                    
                    var actualValue1 = await cache.GetAsync(key1) ?? throw new NullReferenceException(); 
                    Assert.IsTrue(expectedValue1.SequenceEqual(actualValue1));

                    var actualValue2 = await cache.GetAsync(key2);
                    Assert.IsNull(actualValue2);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(logLocation))
                    Directory.Delete(logLocation, true);
            }
        }
    }
}

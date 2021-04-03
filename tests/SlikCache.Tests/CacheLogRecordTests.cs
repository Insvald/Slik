using Microsoft.Extensions.Caching.Distributed;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class CacheLogRecordTests
    {
        [TestMethod]
        public void UntypedEquals_Null_ReturnsFalse()
        {
            var testRecord = new CacheLogRecord(CacheOperation.Remove, "key", null, null);

            bool result = testRecord.Equals((object?)null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void UntypedEquals_NotCacheLogRecord_ReturnsFalse()
        {
            var testRecord = new CacheLogRecord(CacheOperation.Remove, "key", null, null);

            bool result = testRecord.Equals(new object());

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void UntypedEquals_SimilarCacheLogRecord_ReturnsTrue()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Remove, "key", null, null);
            var testRecord2 = new CacheLogRecord(CacheOperation.Remove, "key", null, null);

            bool result = testRecord1.Equals((object)testRecord2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TypedEquals_Null_ReturnsFalse()
        {
            var testRecord = new CacheLogRecord(CacheOperation.Remove, "key", null, null);

            bool result = testRecord.Equals(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TypedEquals_SimilarCacheLogRecord_ReturnsTrue()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Remove, "key", null, null);
            var testRecord2 = new CacheLogRecord(CacheOperation.Remove, "key", null, null);

            bool result = testRecord1.Equals(testRecord2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TypedEquals_DifferentOperations_ReturnsFalse()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Remove, "key", null, null);
            var testRecord2 = new CacheLogRecord(CacheOperation.Update, "key", null, null);

            bool result = testRecord1.Equals(testRecord2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TypedEquals_DifferentKeys_ReturnsFalse()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Remove, "key1", null, null);
            var testRecord2 = new CacheLogRecord(CacheOperation.Remove, "key2", null, null);

            bool result = testRecord1.Equals(testRecord2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TypedEquals_DifferentValues_ReturnsFalse()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Remove, "key", new byte[] { 1, 2, 3 }, null);
            var testRecord2 = new CacheLogRecord(CacheOperation.Remove, "key", new byte[] { 3, 2, 1 }, null);

            bool result = testRecord1.Equals(testRecord2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TypedEquals_DifferentOptions_ReturnsFalse()
        {
            var testRecord1 = new CacheLogRecord(CacheOperation.Update, "key", null, new DistributedCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset() });
            var testRecord2 = new CacheLogRecord(CacheOperation.Update, "key", null, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1) });

            bool result = testRecord1.Equals(testRecord2);

            Assert.IsFalse(result);
        }
    }
}

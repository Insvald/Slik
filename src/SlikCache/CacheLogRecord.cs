using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Linq;

namespace Slik.Cache
{
    public enum CacheOperation 
    { 
        Update,
        Remove,
        Refresh
    }

    public class CacheLogRecord : IEquatable<CacheLogRecord>
    {
        public CacheLogRecord(CacheOperation operation, string key, byte[] value, DistributedCacheEntryOptions? options = null)
        {
            Operation = operation;
            Key = key;
            Value = value;
            Options = options;
        }

        public CacheOperation Operation { get; }
        public string Key { get; }
        public byte[] Value { get; }
        public DistributedCacheEntryOptions? Options { get; }

        public override bool Equals(object? obj) => (obj is CacheLogRecord record) && Equals(record);

        public override int GetHashCode() => HashCode.Combine(Operation, Key, Value, Options);

        public bool Equals(CacheLogRecord? other) =>
            (other != null) && (Operation == other.Operation) && (Key == other.Key) &&
            BothNullOrEqual(Value, other.Value, (a, b) => a.SequenceEqual(b)) &&
            BothNullOrEqual(Options, other.Options, (a, b) => AreEqualOptions(a, b));        

        private static bool BothNullOrEqual<T>(T? a, T? b, Func<T, T, bool> equality) =>
            ((a == null) && (b == null)) || ((a != null) && (b != null) && equality(a, b));

        public static bool AreEmptyOptions(DistributedCacheEntryOptions? options) =>
            (options == null || (options.AbsoluteExpiration == null && options.AbsoluteExpirationRelativeToNow == null && options.SlidingExpiration == null));

        public static bool AreEqualOptions(DistributedCacheEntryOptions a, DistributedCacheEntryOptions b) =>
            BothNullOrEqual(a.AbsoluteExpiration, b.AbsoluteExpiration, (a, b) => a.Equals(b)) &&
            BothNullOrEqual(a.AbsoluteExpirationRelativeToNow, b.AbsoluteExpirationRelativeToNow, (a, b) => a.Equals(b)) &&
            BothNullOrEqual(a.SlidingExpiration, b.SlidingExpiration, (a, b) => a.Equals(b));        
    }    
}

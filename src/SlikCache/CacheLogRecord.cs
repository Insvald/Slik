using Microsoft.Extensions.Caching.Distributed;
using System;

namespace Slik.Cache
{
    public enum CacheOperation 
    { 
        Update,
        Remove,
        Refresh
    }

    public class CacheLogRecord
    {
        public CacheLogRecord(CacheOperation operation, string key, byte[] value, DistributedCacheEntryOptions? options = null)
        {
            Operation = operation;
            Key = key;
            Value = value;
            Options = options;
        }

        public Guid Id { get; set; }
        public CacheOperation Operation { get; }
        public string Key { get; }
        public byte[] Value { get; }
        public DistributedCacheEntryOptions? Options { get; }        
    }    
}
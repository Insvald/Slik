using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc.V1
{
    [DataContract]
    public class KeyRequest
    {
        [DataMember(Order = 1)]
        public string Key { get; set; } = string.Empty;       
    }

    [DataContract]
    public class ValueResponse
    {
        [DataMember(Order = 1)]
        public byte[] Value { get; set; } = Array.Empty<byte>();
    }

    [DataContract]
    public class SetRequest
    {
        [DataMember(Order = 1)]
        public string Key { get; set; } = string.Empty;
        
        [DataMember(Order = 2)]
        public byte[] Value { get; set; } = Array.Empty<byte>();

        [DataMember(Order = 3)]
        public SetRequestOptions Options { get; set; } = new();
    }

    // wrapper for DistributedCacheEntryOptions
    [DataContract]
    public class SetRequestOptions
    {
        public DateTime? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }

        public DistributedCacheEntryOptions ToDistributedCacheEntryOptions() =>        
            new() 
            {
                AbsoluteExpiration = AbsoluteExpiration,
                AbsoluteExpirationRelativeToNow = AbsoluteExpirationRelativeToNow,
                SlidingExpiration = SlidingExpiration
            };

        public static SetRequestOptions FromDistributedCacheEntryOptions(DistributedCacheEntryOptions options) =>
            new()
            {
                AbsoluteExpiration = options.AbsoluteExpiration?.DateTime,
                AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
                SlidingExpiration = options.SlidingExpiration
            };
    }

    [ServiceContract(Name = "SlikCache")]
    public interface ISlikCacheService
    {
        ValueTask<ValueResponse> Get(KeyRequest request);
        Task Set(SetRequest request);
        Task Refresh(KeyRequest request);
        Task Remove(KeyRequest request);
    }
}
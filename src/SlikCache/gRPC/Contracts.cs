using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc
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
        public DistributedCacheEntryOptions Options { get; set; } = new();
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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc.V1
{
    public class SlikCacheGrpcService : BaseGrpcService, ISlikCacheService
    {
        private readonly IDistributedCache _cache;
                        
        public SlikCacheGrpcService(ILogger<SlikCacheGrpcService> logger, IDistributedCache cache) : base(logger)
        {
            _cache = cache;
        }       

        public async ValueTask<ValueResponse> Get(KeyRequest request)
        {
            LogCallEntrance();
            var result = await _cache.GetAsync(request.Key).ConfigureAwait(false);
            LogCallExit();
            return new ValueResponse { Value = result };
        }

        public async Task Refresh(KeyRequest request)
        {
            LogCallEntrance();
            await _cache.RefreshAsync(request.Key).ConfigureAwait(false);
            LogCallExit();
        }

        public async Task Remove(KeyRequest request)
        {
            LogCallEntrance();
            await _cache.RemoveAsync(request.Key).ConfigureAwait(false);
            LogCallExit();
        }

        public async Task Set(SetRequest request)
        {
            LogCallEntrance();
            await _cache.SetAsync(request.Key, request.Value, request.Options.ToDistributedCacheEntryOptions()).ConfigureAwait(false);
            LogCallExit();
        }
    }
}
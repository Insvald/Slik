﻿using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc
{
    public class SlikCacheGrpcService : ISlikCacheService
    {
        private readonly ILogger<SlikCacheGrpcService> _logger;
        private readonly SlikCache _cache;
                        
        internal SlikCacheGrpcService(ILogger<SlikCacheGrpcService> logger, SlikCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        private void LogCallEntrance([CallerMemberName] string caller = "")
        {
            _logger.LogDebug($"Received gRPC call: {nameof(SlikCacheGrpcService)}.{caller}");
        }

        private void LogCallExit([CallerMemberName] string caller = "")
        {
            _logger.LogDebug($"Finished gRPC call: {nameof(SlikCacheGrpcService)}.{caller}");
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
            await _cache.SetAsync(request.Key, request.Value, request.Options).ConfigureAwait(false);
            LogCallExit();
        }
    }
}
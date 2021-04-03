using DotNext.Net.Cluster.Consensus.Raft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.IO;

namespace Slik.Cache.Tests
{
    internal static class SlikCacheHelper
    {
        // Creating config and logger for cache
        public static SlikCache InitCache(string? path = null)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var cacheConfigration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { SlikCache.LogLocationConfiguration, path } })
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            return new SlikCache(cacheConfigration, loggerFactory); // without SlikRouter it is in offline mode
        }

        public static void DestroyCache(SlikCache cache)
        {
            string logLocation = cache.LogLocation;
            cache.Dispose();
            Directory.Delete(logLocation, true);
        }
    }
}

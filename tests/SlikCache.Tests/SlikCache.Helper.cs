using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            var optionsMock = new Mock<IOptions<SlikOptions>>();
            optionsMock.SetupGet(o => o.Value).Returns(new SlikOptions { DataFolder = path });
            
            return new SlikCache(optionsMock.Object, loggerFactory); // without SlikRouter it is in offline mode
        }

        public static void DestroyCache(SlikCache cache)
        {
            string logLocation = cache.LogLocation;
            cache.Dispose();
            Directory.Delete(logLocation, true);
        }
    }
}

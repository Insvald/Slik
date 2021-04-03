using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Slik.Cache
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseSlik(this IHostBuilder builder, bool externalApi = false) =>
            builder
                .ConfigureServices(services => services
                    .UsePersistenceEngine<IDistributedCache, SlikCache>()
                    .AddHostedService<SlikRouter>())
                .JoinCluster();
    }
}

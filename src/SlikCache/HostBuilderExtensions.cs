using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;
using Slik.Cache.Grpc;
using System.IO.Compression;

namespace Slik.Cache
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseSlik(this IHostBuilder builder, bool externalApi = false) =>
            builder
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .Configure((context, app) =>
                    {
                        app.UseConsensusProtocolHandler();

                        if (externalApi)
                        {
                            (context.HostingEnvironment.IsDevelopment() ? app.UseDeveloperExceptionPage() : app)
                                .UseRouting()
                                //.UseAuthentication()
                                //.UseAuthorization()
                                .UseEndpoints(endpoints => 
                                {
                                    endpoints.MapGrpcService<SlikCacheGrpcService>();
                                    //endpoints.MapGet("/", async context => await context.Response.WriteAsync("gRPC endpoint"));
                                });
                        }
                    })
                    .ConfigureKestrel((context, options) => options.ListenLocalhost(
                        context.Configuration.GetValue<int>("port"),
                        opt => opt.Protocols = externalApi ? HttpProtocols.Http2 : HttpProtocols.Http1AndHttp2)))
                .ConfigureServices(services =>
                {
                    services
                        .UsePersistenceEngine<IDistributedCache, SlikCache>()
                        .AddHostedService<SlikRouter>();

                    if (externalApi)
                        services.AddCodeFirstGrpc(config => config.ResponseCompressionLevel = CompressionLevel.Optimal);
                })
                .JoinCluster();
    }
}

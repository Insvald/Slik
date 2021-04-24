using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Server;
using Slik.Cache.Grpc.V1;
using Slik.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Slik.Cache
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseSlik(this IHostBuilder builder, SlikOptions? cacheOptions = null)
        {
            cacheOptions ??= new SlikOptions
            {
                Host = new IPEndPoint(IPAddress.Loopback, SlikOptions.DefaultPort)
            };

            // updating configuration
            string folder = string.IsNullOrEmpty(cacheOptions.DataFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik")
                : cacheOptions.DataFolder;

            var nodeConfiguration = new Dictionary<string, string>
            {
                { "folder", folder },
                { "cacheLogLocation", Path.Combine(folder, "Cache") },
                { "port", cacheOptions.Host.Port.ToString() },
                { "protocolVersion", "http2" }
            };

            int i = 0;
            foreach (string member in cacheOptions.Members)
                nodeConfiguration[$"members:{i++}"] = member;

            builder
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(nodeConfiguration))

                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .Configure((context, app) =>
                    {
                        app.UseConsensusProtocolHandler();

                        if (cacheOptions.EnableGrpcApi)
                        {
                            (context.HostingEnvironment.IsDevelopment() ? app.UseDeveloperExceptionPage() : app)
                                .UseRouting()
                                .UseEndpoints(endpoints =>
                                {
                                    endpoints.MapGrpcService<SlikMembershipGrpcService>();
                                    endpoints.MapGrpcService<SlikCacheGrpcService>();
                                    //endpoints.MapGet("/", async context => await context.Response.WriteAsync("gRPC endpoint"));
                                });
                        }
                    })
                    .ConfigureKestrel((context, options) =>
                    {
                        var certifier = options.ApplicationServices.GetRequiredService<ICommunicationCertifier>();
                        options.ConfigureHttpsDefaults(options => certifier.SetupServer(options));

                        // TODO check why we can't use Listen() in both cases
                        if (cacheOptions.Host.Address == IPAddress.Loopback)
                            options.ListenLocalhost(cacheOptions.Host.Port, opt => opt.UseHttps().Protocols = HttpProtocols.Http2);
                        else
                            options.Listen(cacheOptions.Host, opt => opt.UseHttps().Protocols = HttpProtocols.Http2);

                    }))
                .ConfigureServices(services =>
                {
                    if (cacheOptions.CertificateOptions.UseSelfSigned)
                        services.AddSingleton<ICommunicationCertifier, SelfSignedCertifier>();
                    else
                        services.AddSingleton<ICommunicationCertifier, CaSignedCertifier>();

                    services
                        .AddTransient<ICertificateGenerator, CertificateGenerator>()
                        .Configure<CertificateOptions>(options => cacheOptions.CertificateOptions.CopyTo(options))
                        .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                        .Configure<SlikOptions>(options => cacheOptions.CopyTo(options))
                        .UsePersistenceEngine<IDistributedCache, SlikCache>()                        
                        .AddHostedService<SlikRouter>();

                    if (cacheOptions.EnableGrpcApi)
                    {
                        services
                            .AddSingleton<SlikMembershipHandler>()
                            .AddSingleton<ISlikMembership>(ServiceProviderServiceExtensions.GetRequiredService<SlikMembershipHandler>)
                            .AddCodeFirstGrpc();
                    }
                })
                .JoinCluster();

            return builder;
        }
    }
}
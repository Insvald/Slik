using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Server;
using Slik.Security;
using System;
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

            builder
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .Configure((context, app) =>
                    {
                        app.UseConsensusProtocolHandler();

                        if (cacheOptions.EnableGrpcApi == true)
                        {
                            (context.HostingEnvironment.IsDevelopment() ? app.UseDeveloperExceptionPage() : app)
                                .UseRouting()
                                .UseEndpoints(endpoints =>
                                {
                                    endpoints.MapGrpcService<Grpc.V1.SlikCacheGrpcService>();
                                    //endpoints.MapGet("/", async context => await context.Response.WriteAsync("gRPC endpoint"));
                                });
                        }
                    })
                    .ConfigureKestrel((context, options) =>
                    {
                        // modifying configuration
                        string folder = string.IsNullOrEmpty(cacheOptions.DataFolder)
                            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik")
                            : cacheOptions.DataFolder;

                        context.Configuration["folder"] = folder;
                        context.Configuration["cacheLogLocation"] = Path.Combine(folder, "Cache");
                        context.Configuration["port"] = cacheOptions.Host.Port.ToString();
                        context.Configuration["protocolVersion"] = "http2";

                        int i = 0;
                        foreach (string member in cacheOptions.Members)
                            context.Configuration[$"members:{i++}"] = member;

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
                    if (cacheOptions.CertificateOptions.SelfSignedUsage == SelfSignedUsage.None)
                        services.AddSingleton<ICommunicationCertifier, CaSignedCertifier>();
                    else
                        services.AddSingleton<ICommunicationCertifier, SelfSignedCertifier>();

                    services                        
                        .AddTransient<ICertificateGenerator, CertificateGenerator>()
                        .Configure<CertificateOptions>(options => cacheOptions.CertificateOptions.CopyTo(options))                                                
                        .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                        .Configure<SlikOptions>(options => cacheOptions.CopyTo(options))
                        .UsePersistenceEngine<IDistributedCache, SlikCache>()
                        .AddHostedService<SlikRouter>();

                    if (cacheOptions.EnableGrpcApi)
                        services.AddCodeFirstGrpc();
                })
                .JoinCluster();

            return builder;
        }        
    }    
}
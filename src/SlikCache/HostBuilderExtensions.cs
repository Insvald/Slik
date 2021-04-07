using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;
using Slik.Cache.Grpc;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Cache
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseSlik(this IHostBuilder builder,
            SlikOptions? cacheOptions = null,
            Action<HttpsConnectionAdapterOptions>? serverOptionsSetter = null,
            Action<SslClientAuthenticationOptions>? clientOptionsSetter = null)
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
                                // TODO
                                //.UseAuthentication()
                                //.UseAuthorization()
                                .UseEndpoints(endpoints =>
                                {
                                    endpoints.MapGrpcService<SlikCacheGrpcService>();
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

                        options.ConfigureHttpsDefaults(options =>
                        {
                            if (serverOptionsSetter != null)
                            {
                                serverOptionsSetter(options);
                            }
                            else
                            {
                                // load embedded certificate
                                using var rawCertificate = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(HostBuilderExtensions), "node.pfx");
                                var memoryStream = new MemoryStream(1024);
                                rawCertificate?.CopyTo(memoryStream);
                                memoryStream.Seek(0, SeekOrigin.Begin);
                                options.ServerCertificate = new X509Certificate2(memoryStream.ToArray(), "1234");
                            }
                        });

                        // TODO check why we can't use Listen() in both cases
                        if (cacheOptions.Host.Address == IPAddress.Loopback)
                            options.ListenLocalhost(cacheOptions.Host.Port, opt => opt.UseHttps().Protocols = HttpProtocols.Http2);
                        else
                            options.Listen(cacheOptions.Host, opt => opt.UseHttps().Protocols = HttpProtocols.Http2);

                    }))
                .ConfigureServices(services =>
                {
                    services
                        .Configure<SlikOptions>(opt => cacheOptions.CopyTo(opt))
                        .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>(_ => new RaftClientHandlerFactory(clientOptionsSetter))
                        .UsePersistenceEngine<IDistributedCache, SlikCache>()
                        .AddHostedService<SlikRouter>();

                    if (cacheOptions.EnableGrpcApi)
                        services.AddCodeFirstGrpc(config => config.ResponseCompressionLevel = CompressionLevel.Optimal);
                })
                .JoinCluster();

            return builder;
        }        
    }    
}
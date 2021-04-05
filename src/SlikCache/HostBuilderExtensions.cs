using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;
using Slik.Cache.Grpc;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Cache
{
    public static class HostBuilderExtensions
    {
        private static X509Certificate2 LoadCertificate()
        {
            using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(HostBuilderExtensions), "node.pfx");
            var memoryStream = new MemoryStream(1024);
            rawCertificate?.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return new X509Certificate2(memoryStream.ToArray(), "1234");
        }

        public static IHostBuilder UseSlik(this IHostBuilder builder, bool enableGrpcApi = false) =>
            builder
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .Configure((context, app) =>
                    {
                        app.UseConsensusProtocolHandler();

                        if (enableGrpcApi)
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
                    .ConfigureKestrel((context, options) =>
                    {
                        context.Configuration["protocolVersion"] = "http2";

                        var certificate = LoadCertificate();

                        options.ConfigureHttpsDefaults(options =>
                        {
                            options.ServerCertificateSelector = (_, __) => certificate;
                            options.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                            options.ClientCertificateValidation = (_, __, ___) => true;
                        });

                        options.ListenLocalhost(
                            context.Configuration.GetValue<int>("port"),
                            opt => opt.UseHttps().Protocols = HttpProtocols.Http2);
                        
                    }))
                .ConfigureServices(services =>
                {
                    services
                        .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
                        .UsePersistenceEngine<IDistributedCache, SlikCache>()
                        .AddHostedService<SlikRouter>();

                    if (enableGrpcApi)
                        services.AddCodeFirstGrpc(config => config.ResponseCompressionLevel = CompressionLevel.Optimal);
                })
                .JoinCluster();
    }

    // https://sakno.github.io/dotNext/features/cluster/aspnetcore.html
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        public HttpMessageHandler CreateHandler(string name)
        {
            var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
            handler.SslOptions.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;            
            return handler;
        }
    }
}

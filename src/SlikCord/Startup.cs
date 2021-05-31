using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Slik.Cord.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Slik.Cord
{
    internal class Startup
    {
        private readonly IConfiguration _config;

        internal class CommandLineOptions
        {
            [Option(shortName: 'p', longName: "port", Required = false, HelpText = "Port to use for the local instance.", Default = 3100)]
            public int Port { get; set; }

            [Option(shortName: 'f', longName: "folder", Required = false, HelpText = "Folder for local data.", Default = null)]
            public string? Folder { get; set; }

            [Option(shortName: 'x', longName: "external", Required = false, HelpText = "Use external containerd instance.", Default = false)]
            public bool UseExternalContainerdInstance { get; set; }

            //[Option(shortName: 's', longName: "use-self-signed", Required = false, HelpText = "Use self-signed certificates", Default = false)]
            //public bool UseSelfSignedCertificates { get; set; }
        }

        public static async Task<int> StartHostAsync(CommandLineOptions options)
        {
            options.Folder ??= Path.Combine(
                UnixUtils.IsUnixFamily() 
                    ? "/" 
                    : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Slik", "Cord");

            using var logger = CreateLogger(Path.Combine(options.Folder, "Logs"));

            logger.Information($"Slik Cord v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {options.Port}");

            try
            {
                var config = new Dictionary<string, string>
                {
                    { nameof(ContainerdClientOptions.UseExternalContainerdInstance), options.UseExternalContainerdInstance.ToString() }
                };

                await Host
                    .CreateDefaultBuilder()
                    .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(config))
                    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>().ConfigureKestrel((_, serverOptions) => 
                    {
                        serverOptions.ListenAnyIP(options.Port);
                    }))                    
                    .Build()
                    .RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, $"Fatal error occured: {ex.Message} The node is closing.");
                return -1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        
        public Startup(IConfiguration config)
        {
            _config = config;
        }

        private static Logger CreateLogger(string pathForLogs) =>
            new LoggerConfiguration()
                .Enrich.WithThreadId()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.File(Path.Combine(pathForLogs, $"{Process.GetCurrentProcess().ProcessName}-.log"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} ({ThreadId}) [{Level:u3}] {Message:lj} {NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day)
                .WriteTo.Console(LogEventLevel.Information, "[{Level:u3}] {Message:lj}{NewLine}")
                .CreateLogger();

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .Configure<ContainerdClientOptions>(_config)                
                .AddSingleton<ContainerdClient>()
                .AddSingleton<IContainerdClient>(p => p.GetRequiredService<ContainerdClient>())
                .AddHostedService(p => p.GetRequiredService<ContainerdClient>())
                // adding grpc clients
                .AddTransient(services => new Containerd.Services.Containers.V1.Containers.ContainersClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Content.V1.Content.ContentClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Diff.V1.Diff.DiffClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Events.V1.Events.EventsClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Images.V1.Images.ImagesClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Introspection.V1.Introspection.IntrospectionClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Leases.V1.Leases.LeasesClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Namespaces.V1.Namespaces.NamespacesClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Snapshots.V1.Snapshots.SnapshotsClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Tasks.V1.Tasks.TasksClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Events.Ttrpc.V1.Events.EventsClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                .AddTransient(services => new Containerd.Services.Version.V1.Version.VersionClient(services.GetRequiredService<IContainerdClient>().ClientChannel))
                // finished adding grpc clients
                .AddGrpc();
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            (env.IsDevelopment() ? app.UseDeveloperExceptionPage() : app)
                .UseRouting()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapGrpcService<VersionService>();
                    endpoints.MapGrpcService<ContainerService>();
                    endpoints.MapGrpcService<ImageService>();
                    endpoints.MapGrpcService<IntrospectionService>();
                    endpoints.MapGrpcService<ContentService>();
                    endpoints.MapGrpcService<EventService>();
                    endpoints.MapGrpcService<NamespaceService>();
                    endpoints.MapGrpcService<TaskService>();
                    endpoints.MapGrpcService<DiffService>();
                    endpoints.MapGrpcService<LeaseService>();
                    endpoints.MapGrpcService<SnapshotService>();
                    endpoints.MapGrpcService<TtrpcEventService>();

                    endpoints.MapGet("/", async context => await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client."));
                });
        }
    }
}

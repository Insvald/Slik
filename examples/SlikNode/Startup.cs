using CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Slik.Cache;
using Slik.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SlikCache.IntegrationTests")]
namespace Slik.Node
{
    internal static class Startup
    {
        internal class CommandLineOptions
        {
            [Option(shortName: 'p', longName: "port", Required = false, HelpText = "Port to use for the local instance.", Default = SlikOptions.DefaultPort)]
            public int Port { get; set; }

            [Option(shortName: 'm', longName: "members", Required = false, HelpText = "List of cluster members.")]
            public string Members { get; set; } = "";

            [Option(shortName: 'f', longName: "folder", Required = false, HelpText = "Folder for cache data.", Default = null)]
            public string? Folder { get; set; }

            [Option(shortName: 'a', longName: "api", Required = false, HelpText = "Enable external gRPC API. Required for dynamic adding/removing of members.", Default = false)]
            public bool EnableGrpcApi { get; set; }

            [Option(shortName: 't', longName: "testCache", Required = false, HelpText = "Enable test cache consumer", Default = false)]
            public bool EnableConsumer { get; set; }

            [Option(shortName: 's', longName: "use-self-signed", Required = false, HelpText = "Use self-signed certificates", Default = false)]
            public bool UseSelfSignedCertificates { get; set; }
        }

        public static async Task<int> StartHostAsync(CommandLineOptions options)
        {
            options.Folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik");

            using var logger = CreateLogger(Path.Combine(options.Folder, "Logs"));

            logger.Information($"Slik Node v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {options.Port}");

            try
            {
                using var nodeCertificate = options.UseSelfSignedCertificates ? null : LoadCertificate("node.pfx");
                var host = new IPEndPoint(IPAddress.Loopback, options.Port);

                await Host
                    .CreateDefaultBuilder()
                    .UseSerilog(logger)
                    .ConfigureWebHostDefaults(webBuilder => webBuilder.ConfigureServices(services =>
                    {
                        if (options.EnableConsumer)
                            services.AddHostedService<CacheConsumer>();
                    }))
                    .UseSlik(new SlikOptions
                    {
                        Host = host,
                        Members = AddClusterMembers(string.IsNullOrEmpty(options.Members) ? $"https://{host}" : options.Members),
                        EnableGrpcApi = options.EnableGrpcApi,
                        DataFolder = options.Folder,
                        CertificateOptions = new CertificateOptions
                        {
                            UseSelfSigned = options.UseSelfSignedCertificates,
                            ClientCertificate = nodeCertificate,
                            ServerCertificate = nodeCertificate
                        }
                    })
                    .Build()
                    .RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, $"Fatal error occured: {ex.Message}. The node is closing.");
                return -1;
            }
        }

        internal static X509Certificate2 LoadCertificate(string certificateName)
        {
            using var rawCertificate = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Startup), certificateName);

            var memoryStream = new MemoryStream(1024);
            rawCertificate?.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return new X509Certificate2(memoryStream.ToArray(), "1234");
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

        private static IEnumerable<string> AddClusterMembers(string membersArgument)
        {
            try
            {
                List<string> result = new();

                string[] memberList = membersArgument.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < memberList.Length; i++)
                {
                    result.Add(memberList[i].TrimStart().ToLower().StartsWith("http")
                        ? memberList[i].Trim()
                        : $"https://{memberList[i].Trim()}");
                }

                return result;
            }
            catch
            {
                throw new Exception($"Error parsing cluster members' list: '{membersArgument}'");
            }
        }
    }
}

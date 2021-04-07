using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Slik.Cache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Slik.Node
{
    internal static class Startup
    {
        public static async Task<int> StartHostAsync(int port, string members, string? dataFolder, bool enableGrpcApi, bool enableConsumer)
        {
            dataFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik");

            using var logger = CreateLogger(Path.Combine(dataFolder, "Logs"));

            logger.Information($"Slik Node v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {port}");
            
            try
            {
                await Host
                    .CreateDefaultBuilder()
                    .UseSerilog(logger)
                    .ConfigureWebHostDefaults(webBuilder => webBuilder.ConfigureServices(services =>
                    {
                        if (enableConsumer)
                            services.AddHostedService<CacheConsumer>();
                    }))
                    .UseSlik(new SlikOptions 
                    { 
                        Host = new IPEndPoint(IPAddress.Loopback, port),
                        Members = AddClusterMembers(members),
                        EnableGrpcApi = enableGrpcApi,
                        DataFolder = dataFolder
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

                string[] memberList = membersArgument.Split(",", StringSplitOptions.RemoveEmptyEntries);

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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
using System.Reflection;
using System.Threading.Tasks;

namespace Slik.Node
{
    internal sealed class Startup
    {
        public async Task<int> StartHostAsync(int port, string members, string? folder, bool enableGrpcApi, bool enableConsumer)
        {
            folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik");

            using var logger = CreateLogger(Path.Combine(folder, "Logs"));

            logger.Information($"Slik Node v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {port}");
            
            try
            {
                var internalConfig = new Dictionary<string, string>
                {
                    { "port", port.ToString() },
                    { "cacheLogLocation", Path.Combine(folder, "Cache") },
                    { "folder", folder }
                };

                AddClusterMembers(members, internalConfig);

                await Host
                    .CreateDefaultBuilder()
                    .UseSerilog(logger)
                    .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(internalConfig))
                    .ConfigureWebHostDefaults(webBuilder => webBuilder.ConfigureServices(services =>
                    {
                        if (enableConsumer)
                            services.AddHostedService<CacheConsumer>();
                    }))
                    .UseSlik(enableGrpcApi)
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

        private Logger CreateLogger(string pathForLogs) =>        
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

        private void AddClusterMembers(string membersArgument, IDictionary<string, string> config)
        {
            try
            {
                string[] memberList = membersArgument.Split(",", StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < memberList.Length; i++)
                {
                    config[$"members:{i}"] = memberList[i].TrimStart().ToLower().StartsWith("http")
                        ? memberList[i].Trim()
                        : $"https://{memberList[i].Trim()}";
                }
            }
            catch
            {
                throw new Exception($"Error parsing cluster members' list: '{membersArgument}'");
            }
        }
    }
}

using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Slik.Cache;
using Slik.Node;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

var config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();

string port = config["port"];
if (string.IsNullOrEmpty(port))
{
    Console.WriteLine("No port specified. The node is closing.");
    Environment.ExitCode = -2;
    return;
}

// expanding environment variable on Ubuntu doesn't work for some reason
string dataFolder = config["folder"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Slik");
string logsFolder = Path.Combine(dataFolder, "Logs");
string cacheFolder = Path.Combine(dataFolder, "Cache");

using var logger = Slik.Node.Startup.SetupSerilog(logsFolder);
logger.Information($"Slik Node v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {port}");

try
{
    await Host
        .CreateDefaultBuilder(args)
        .UseSerilog(logger)
        .ConfigureAppConfiguration(builder => builder.AddJsonFile("appsettings.json").AddInMemoryCollection(new[] 
        { 
            new KeyValuePair<string, string>("cacheLogLocation", cacheFolder),
            new KeyValuePair<string, string>("folder", dataFolder) // in case it has been changed
        }))
        .ConfigureWebHostDefaults(webBuilder => webBuilder            
            .Configure(app => app.UseConsensusProtocolHandler())
            .ConfigureServices(services => services.AddHostedService<CacheConsumer>())
            .ConfigureKestrel((context, options) =>  options.ListenLocalhost(context.Configuration.GetValue<int>("port"))))
        .UseSlik()
        .Build()
        .RunAsync();
}
catch (Exception ex)
{
    logger.Fatal(ex, $"Fatal error occured: {ex.Message}. The node is closing.");
    Environment.ExitCode = -1;
}
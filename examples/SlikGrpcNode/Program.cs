using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Slik.Cache;
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

using var logger = Slik.GrpcNode.Startup.SetupSerilog(logsFolder);
logger.Information($"Slik Node v{Assembly.GetExecutingAssembly().GetName().Version}. Listening on port {port}");

try
{
    await Host
        .CreateDefaultBuilder(args)
        .UseSerilog(logger)
        .ConfigureAppConfiguration(builder => builder.AddJsonFile("appsettings.json").AddInMemoryCollection(new[] 
        { 
            new KeyValuePair<string, string>("cacheLogLocation", cacheFolder),
            new KeyValuePair<string, string>("protocolVersion", "http2"), // TODO check if required       
            new KeyValuePair<string, string>("folder", dataFolder) // in case it has been changed
        }))
        .UseSlik(externalApi: true)
        .Build()
        .RunAsync();
}
catch (Exception ex)
{
    logger.Fatal(ex, $"Fatal error occured: {ex.Message}. The node is closing.");
    Environment.ExitCode = -1;
}
using CommandLine;
using Slik.Node;
using System.Threading.Tasks;

await Parser
    .Default
    .ParseArguments<CommandLineOptions>(args)
    .MapResult(
        async (CommandLineOptions opts) => await Startup.StartHostAsync(opts.Port, opts.Members, opts.Folder, opts.EnableGrpcApi, opts.EnableConsumer),
        errs =>Task.FromResult(-1)); 

public class CommandLineOptions
{
    [Option(shortName: 'p', longName: "port", Required = true, HelpText = "Port to use for the local instance.")]
    public ushort Port { get; set; }

    [Option(shortName: 'm', longName: "members", Required = true, HelpText = "List of cluster members.")]
    public string Members { get; set; } = "";

    [Option(shortName: 'f', longName: "folder", Required = false, HelpText = "Folder for cache data.", Default = null)]
    public string? Folder { get; set; }

    [Option(shortName: 'a', longName: "api", Required = false, HelpText = "Enable external gRPC API", Default = false)]
    public bool EnableGrpcApi { get; set; }

    [Option(shortName: 't', longName: "testCache", Required = false, HelpText = "Enable test cache consumer", Default = false)]
    public bool EnableConsumer { get; set; }
}
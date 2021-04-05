using CommandLine;
using Slik.GrpcNode;
using System.Threading.Tasks;

await Parser
    .Default
    .ParseArguments<CommandLineOptions>(args)
    .MapResult(
        async (CommandLineOptions opts) => await new Startup().StartHostAsync(opts.Port, opts.Members, opts.Folder),
        errs => Task.FromResult(-1));

public class CommandLineOptions
{
    [Option(shortName: 'p', longName: "port", Required = true, HelpText = "Port to use for the local instance.")]
    public ushort Port { get; set; }

    [Option(shortName: 'm', longName: "members", Required = true, HelpText = "List of cluster members.")]
    public string Members { get; set; } = "";

    [Option(shortName: 'f', longName: "folder", Required = false, HelpText = "Folder for cache data.", Default = null)]
    public string? Folder { get; set; } = "";
}
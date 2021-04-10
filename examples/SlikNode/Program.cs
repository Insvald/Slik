using CommandLine;
using Slik.Node;
using System.Threading.Tasks;

await Parser
    .Default
    .ParseArguments<Startup.CommandLineOptions>(args)
    .MapResult(
        async (Startup.CommandLineOptions options) => await Startup.StartHostAsync(options),
        errs =>Task.FromResult(-1)); 

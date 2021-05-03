using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cord
{
    public interface IContainerdClient
    {
        GrpcChannel ClientChannel { get; }
    }

    public class ContainerdClientOptions
    {
        public bool UseExternalContainerdInstance { get; set; }
    }

    public class ContainerdClient : IHostedService, IContainerdClient
    {
        private readonly ILogger<ContainerdClient> _logger;
        private readonly ContainerdClientOptions _options;
        private Process? _process;

        public string SourceArchive { get; set; } = "assets/containerd-1.4.4-linux-amd64.tar.gz";
        public string DestinationFolder { get; set; } = "/home/.containerd";
        public const string UnixDomainSocket = "/run/containerd/containerd.sock";

        public ContainerdClient(ILogger<ContainerdClient> logger, IOptions<ContainerdClientOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }        

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.UseExternalContainerdInstance)
            {
                // check if exists
                if (!Directory.Exists(DestinationFolder))
                {
                    _logger.LogDebug($"Directory '{DestinationFolder}' not found, creating it.");
                    Directory.CreateDirectory(DestinationFolder);

                    // unpack 
                    _logger.LogDebug($"Unzipping '{SourceArchive}' to '{DestinationFolder}'.");
                    using var stream = File.OpenRead(SourceArchive);
                    using var reader = ReaderFactory.Open(stream);
                    while (reader.MoveToNextEntry())
                        if (!reader.Entry.IsDirectory)
                            reader.WriteEntryToDirectory(DestinationFolder, new ExtractionOptions
                            {
                                ExtractFullPath = false,
                                Overwrite = true
                            });

                    // need to give execute permissions in UNIX systems
                    if (UnixUtils.IsUnixFamily())
                    {
                        _logger.LogDebug($"Assigning execute permissions for containerd.");

                        using var process = Process.Start(new ProcessStartInfo
                        {
                            //RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            FileName = "/bin/bash",
                            Arguments = $"-c \"chmod +x {DestinationFolder}/containerd\""
                        }) ?? throw new Exception("Chmod process was not started successfully.");

                        process.WaitForExit();

                        if (process.ExitCode != 0)
                            throw new Exception($"Chmod error code {process.ExitCode}");
                    }

                    _logger.LogDebug($"Copying config to '{DestinationFolder}'");
                    File.Copy("assets/config.toml", Path.Combine(DestinationFolder, "config.toml"));
                }
                else
                    _logger.LogDebug($"Directory '{DestinationFolder}' already exists, skipping unzip.");

                _logger.LogInformation("Starting containerd process");

                // run with our config.toml
                _process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(DestinationFolder, "containerd"),
                    Arguments = "--config config.toml",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = DestinationFolder
                    //RedirectStandardOutput = true
                })
                    ?? throw new Exception("Failure starting containerd process");
            }

            return Task.CompletedTask;
        }

        private readonly Lazy<GrpcChannel> _clientChannel = new(() => GrpcChannel.ForAddress($"http://localhost", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                    try
                    {
                        await socket.ConnectAsync(new UnixDomainSocketEndPoint(UnixDomainSocket), cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            }
        }));

        public GrpcChannel ClientChannel => _clientChannel.Value;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_clientChannel.IsValueCreated)
                _clientChannel.Value.Dispose();

            if (!_options.UseExternalContainerdInstance)
            {
                _logger.LogInformation("Stopping containerd process");
                _process!.Kill(true);
            }

            return Task.CompletedTask;
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    public static class SlikCordContainer
    {
#if NET5_0
        private const string NetFramework = "5.0";
        public const ushort HostPort = 3099;
#else
        private const string NetFramework = "6.0";
        public const ushort HostPort = 3098;
#endif

        private static bool RecreateContainer = true;
        
        public static readonly string ImageName = $"test-slik-cord:{NetFramework}";
        public static readonly string ContainerId = $"test-slik-cord-{NetFramework}";

        private static Task? _prepareTask;
        private static bool _isContainerReady;

        public static async ValueTask EnsureReady()
        { 
            if (!_isContainerReady && _prepareTask != null && !_prepareTask.IsCompleted)
            {
                await _prepareTask;
            }

            if (!_isContainerReady)
                throw new Exception("Container is not ready");
        }

        static SlikCordContainer()
        {
            var docker = new DockerProcess();
            bool exists = docker.DoesContainerExistAsync(ContainerId).Result;
            
            if (RecreateContainer || !exists)
            {
                _prepareTask = PrepareContainerAsync();
            }
        }

        private static async Task PrepareContainerAsync()
        {
            var docker = new DockerProcess();

            Console.WriteLine("Cleaning up previously allocated container.");
            await RemoveContainerAsync();

            Console.WriteLine($"Building a new image '{ImageName}'.");
            await docker.BuildAsync(
                tag: ImageName,
                folder: "..\\..\\..\\..\\..",
                "src/SlikCord/Dockerfile",
                $"FRAMEWORK={NetFramework}");

            Console.WriteLine($"Running the container '{ContainerId}'.");
            await docker.RunAsync(ContainerId, ImageName, $"{HostPort}:80");

            Console.WriteLine("Waiting for the container.");
            await WaitForHttpEndpointAsync(TimeSpan.FromSeconds(10));
        }

        private static async Task WaitForHttpEndpointAsync(TimeSpan timeout)
        {
            using var client = new HttpClient { Timeout = timeout };
            using var cts = new CancellationTokenSource(timeout);

            // waiting for http endpoint
            do
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{HostPort}");
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        _isContainerReady = true;
                        break;
                    }
                }
                catch (HttpRequestException e) when (e.InnerException is SocketException socketEx && socketEx.ErrorCode == 10061)
                {
                    // "No connection could be made because the target machine actively refused it"
                    // means that the container is still warming up
                    continue;
                }
                catch (HttpRequestException e) when (e.InnerException is IOException ioEx && (uint)ioEx.HResult == 0x80131620)
                {
                    // "The response ended prematurely."
                    // for some reason GET handler doesn't work properly
                    _isContainerReady = true;
                    break;
                }
            } while (!cts.IsCancellationRequested);

            // http becomes available a little bit early, need to wait more for gRPC channel
            await Task.Delay(2000);
        }

        public static async Task RemoveContainerAsync()
        {
            var docker = new DockerProcess();

            try
            {                
                await docker.StopAsync(ContainerId);                
            }
            catch 
            {
                // container not found
            }

            try
            {
                await docker.RemoveContainerAsync(ContainerId);
            }
            catch
            {
                // container not found
            }
        }
    }
}

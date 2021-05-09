using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    public abstract class ServiceTestsBase
    {
        private static readonly bool _isCiEnvironment;
        private static readonly GrpcChannel _channel;

        protected static GrpcChannel Channel
        {
            get 
            {
                if (!_isCiEnvironment)
                    SlikCordContainer.EnsureReady().AsTask().Wait();

                return _channel;
            }
        }

        protected static Metadata Headers { get; }

        static ServiceTestsBase()
        {
            // GitHub Actions set CI = true
            _isCiEnvironment = Environment.ExpandEnvironmentVariables("%CI%").Equals("true", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"{(_isCiEnvironment ? "Running" : "Not running")} in CI environment");

            //_channel = GrpcChannel.ForAddress($"http://{(_isCiEnvironment ? "containerd" : "localhost")}:{SlikCordContainer.HostPort}");
            _channel = GrpcChannel.ForAddress($"http://localhost:{SlikCordContainer.HostPort}");
            Headers = new Metadata { { "containerd-namespace", "test-slik-cord" } };
            //Headers = new Metadata { { "containerd-namespace", "default" } };                       
        }       

        [ClassCleanup]
        public static async Task CleanupAsync()
        {
            await _channel.ShutdownAsync();
            _channel.Dispose();
        }
    }
}

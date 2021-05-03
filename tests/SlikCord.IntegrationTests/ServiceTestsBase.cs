using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestEnvironment.Docker;

namespace Slik.Cord.IntegrationTests
{
    public class ServiceTestsBase
    {
        protected static readonly SlikCordFixture Fixture = new();

        [ClassCleanup]
        public static async Task CleanupAsync() => await Fixture.DisposeAsync();

        protected class SlikCordFixture : IAsyncDisposable
        {
            private readonly DockerEnvironment _environment;
            private const ushort HostPort = 1234;

            public Uri Address { get; }
            public GrpcChannel Channel { get; }
            public Metadata Headers { get; }

            public SlikCordFixture()
            {
                Address = new Uri($"http://localhost:{HostPort}");
                Channel = GrpcChannel.ForAddress(Address);
                Headers = new Metadata { { "containerd-namespace", "test" } };

                // start SlikCord in a docker container
                _environment = new DockerEnvironmentBuilder()
                    .SetName("slik")
                    .AddFromDockerfile("cord",
                        "src/SlikCord/Dockerfile",
                        context: "..\\..\\..\\..\\..",
                        containerWaiter: new FuncContainerWaiter(async _ =>
                        {
                            try
                            {
                                var client = new Containerd.Services.Version.V1.Version.VersionClient(Channel);
                                var response = await client.VersionAsync(new Empty(), Headers);
                                return true;
                            }
                            catch (RpcException e) when (e.Message.Contains("StatusCode=\"Unavailable\""))
                            {
                                return false;
                            }
                        }),
                        ports: new Dictionary<ushort, ushort> { { 80, HostPort } })
                    .Build();

                _environment.Up().Wait();
            }

            public async ValueTask DisposeAsync()
            {
                Channel.Dispose();
                await _environment.Down();
                await _environment.DisposeAsync();
            }
        }
    }
}

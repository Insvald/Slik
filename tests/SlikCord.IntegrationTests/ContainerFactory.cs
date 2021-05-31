using Containerd.Services.Containers.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Specs;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    public class ContainerFactory
    {
        private readonly Containers.ContainersClient _client;
        private readonly Metadata _headers;
        private readonly Container.Types.Runtime _runtime;

        private readonly Dictionary<OSPlatform, Container.Types.Runtime> _supportedRuntimes = new()
        {
            { OSPlatform.Linux, new Container.Types.Runtime { Name = $"io.containerd.runc.v2" }},
            { OSPlatform.Windows, new Container.Types.Runtime { Name = $"io.containerd.runhc.v1" }}
        };

        public ContainerFactory(Containers.ContainersClient client, OSPlatform platform, Metadata headers)
        {
            _client = client;
            _headers = headers;
            _runtime = _supportedRuntimes.TryGetValue(platform, out Container.Types.Runtime? runtime) 
                ? runtime
                : throw new NotSupportedException($"The platform '{platform}' is not supported.");
        }        

        public async Task<Container> CreateContainerAsync(string id, string image)
        {
            var spec = new Spec
            {
                OciVersion = "1.0.0",
                Root = new Root
                {
                    Path = "rootfs"
                },
                Process = new Process
                {
                    Cwd = "/",
                    User = new User
                    {
                        Uid = 0,
                        Gid = 0
                    }
                }
            };

            spec.Process.Args.Add("sh");

            var container = new Container
            {
                Id = id,
                Image = image,
                Runtime = _runtime,
                Spec = Any.Pack(spec, "")
                //Spec = Any.Pack(new Empty())
                // Spec:
                // https://github.com/containerd/containerd/blob/ab963e1cc16a845567a0e3e971775c29c701fcf8/vendor/github.com/opencontainers/runtime-spec/specs-go/config.go#L6
                // https://github.com/opencontainers/runtime-spec/blob/master/schema/test/config/good/minimal-for-start.json             
            };

            var createResponse = await _client.CreateAsync(new CreateContainerRequest { Container = container }, _headers);
            return createResponse.Container;            
        }

        public async Task DeleteContainerAsync(Container container)
        {
            try
            {
                var request = new DeleteContainerRequest { Id = container.Id };
                await _client.DeleteAsync(request, _headers);
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound) { /* already deleted */}
        }
    }
}

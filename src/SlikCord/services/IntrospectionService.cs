using Containerd.Services.Introspection.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class IntrospectionService : Introspection.IntrospectionBase
    {
        private readonly Introspection.IntrospectionClient _client;

        public IntrospectionService(Introspection.IntrospectionClient client)
        {
            _client = client;
        }

        public override async Task<PluginsResponse> Plugins(PluginsRequest request, ServerCallContext context) =>
            await _client.PluginsAsync(request, context.ToCallOptions());

        public override async Task<ServerResponse> Server(Empty request, ServerCallContext context) =>
            await _client.ServerAsync(request, context.ToCallOptions());
    }
}

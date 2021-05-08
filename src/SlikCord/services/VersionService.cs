using Containerd.Services.Version.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class VersionService : Version.VersionBase
    {
        private readonly Version.VersionClient _client;

        public VersionService(Version.VersionClient client)
        {
            _client = client;
        }

        public override async Task<VersionResponse> Version(Empty request, ServerCallContext context) => 
            await _client.VersionAsync(request, context.ToCallOptions());
    }
}

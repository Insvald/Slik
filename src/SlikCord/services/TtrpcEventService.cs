using Containerd.Services.Events.Ttrpc.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class TtrpcEventService : Events.EventsBase
    {
        private readonly Events.EventsClient _client;

        public TtrpcEventService(Events.EventsClient client)
        {
            _client = client;
        }

        public override async Task<Empty> Forward(ForwardRequest request, ServerCallContext context) =>
            await _client.ForwardAsync(request, context.ToCallOptions());
    }
}

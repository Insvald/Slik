using Containerd.Services.Events.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class EventService : Events.EventsBase
    {
        private readonly Events.EventsClient _client;

        public EventService(Events.EventsClient client)
        {
            _client = client;
        }

        public override async Task<Empty> Publish(PublishRequest request, ServerCallContext context) =>
            await _client.PublishAsync(request, context.ToCallOptions());

        public override async Task<Empty> Forward(ForwardRequest request, ServerCallContext context) =>
            await _client.ForwardAsync(request, context.ToCallOptions());

        public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<Envelope> responseStream, ServerCallContext context) =>
            await GrpcStreaming.ClientAsync(request, responseStream, _client.Subscribe, context).ConfigureAwait(false);
    }
}

using Containerd.Services.Events.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class EventServiceTests : ServiceTestsBase
    {
        private readonly Events.EventsClient _client = new(Channel);
        private const string TestTopic = "/test-topic";

        private async Task SubscribeGetOrAssert()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var subscribeRequest = new SubscribeRequest();
            subscribeRequest.Filters.Add($"topic=='{TestTopic}'");
            subscribeRequest.Filters.Add($"namespace=={ContainerdNamespace}");

            using var streamingCall = _client.Subscribe(subscribeRequest, Headers);
            
            await foreach (var _ in streamingCall.ResponseStream.ReadAllAsync(cts.Token))
            {
                return;
            }
        }

        [TestMethod]
        public async Task Subscribe_PublishedEvent_GetsEvent()
        {
            var subscribeTask = SubscribeGetOrAssert();

            var request = new PublishRequest
            {
                Topic = TestTopic,
                Event = Any.Pack(new Empty())
            };

            await _client.PublishAsync(request, Headers);
            
            await subscribeTask;

        }

        [TestMethod]
        public async Task Subscribe_ForwardedEvent_GetsEvent()
        {            
            var subscribeTask = SubscribeGetOrAssert();

            var request = new ForwardRequest
            {
                Envelope = new Envelope
                {
                    Event = Any.Pack(new Empty()),
                    Topic = TestTopic,
                    Namespace = ContainerdNamespace,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                }
            };

            await _client.ForwardAsync(request, Headers);

            await subscribeTask;
        }
    }
}

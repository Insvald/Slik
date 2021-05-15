using Containerd.Services.Leases.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class LeaseService : Leases.LeasesBase
    {
        private readonly Leases.LeasesClient _client;

        public LeaseService(Leases.LeasesClient client)
        {
            _client = client;
        }

        public override async Task<Empty> AddResource(AddResourceRequest request, ServerCallContext context) =>
            await _client.AddResourceAsync(request, context.ToCallOptions());

        public override async Task<CreateResponse> Create(CreateRequest request, ServerCallContext context) =>
            await _client.CreateAsync(request, context.ToCallOptions());

        public override async Task<Empty> Delete(DeleteRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());

        public override async Task<Empty> DeleteResource(DeleteResourceRequest request, ServerCallContext context) =>
            await _client.DeleteResourceAsync(request, context.ToCallOptions());

        public override async Task<ListResponse> List(ListRequest request, ServerCallContext context) =>
            await _client.ListAsync(request, context.ToCallOptions());

        public override async Task<ListResourcesResponse> ListResources(ListResourcesRequest request, ServerCallContext context) =>
            await _client.ListResourcesAsync(request, context.ToCallOptions());
    }
}

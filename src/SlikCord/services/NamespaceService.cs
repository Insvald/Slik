using Containerd.Services.Namespaces.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class NamespaceService : Namespaces.NamespacesBase
    {
        private readonly Namespaces.NamespacesClient _client;

        public NamespaceService(Namespaces.NamespacesClient client)
        {
            _client = client;
        }

        public override async Task<GetNamespaceResponse> Get(GetNamespaceRequest request, ServerCallContext context) =>
            await _client.GetAsync(request, context.ToCallOptions());

        public override async Task<CreateNamespaceResponse> Create(CreateNamespaceRequest request, ServerCallContext context) =>
            await _client.CreateAsync(request, context.ToCallOptions());

        public override async Task<Empty> Delete(DeleteNamespaceRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());

        public override async Task<UpdateNamespaceResponse> Update(UpdateNamespaceRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task<ListNamespacesResponse> List(ListNamespacesRequest request, ServerCallContext context) =>
            await _client.ListAsync(request, context.ToCallOptions());
    }
}

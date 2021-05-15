using Containerd.Services.Containers.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class ContainerService : Containers.ContainersBase
    {
        private readonly Containers.ContainersClient _client;

        public ContainerService(Containers.ContainersClient client)
        {
            _client = client;
        }

        public override async Task<GetContainerResponse> Get(GetContainerRequest request, ServerCallContext context) =>
            await _client.GetAsync(request, context.ToCallOptions());

        public override async Task<ListContainersResponse> List(ListContainersRequest request, ServerCallContext context) =>
            await _client.ListAsync(request, context.ToCallOptions());

        public override async Task ListStream(ListContainersRequest request, IServerStreamWriter<ListContainerMessage> responseStream, ServerCallContext context) =>
            await GrpcStreaming.ClientAsync(request, responseStream, _client.ListStream, context).ConfigureAwait(false);
        
        public override async Task<CreateContainerResponse> Create(CreateContainerRequest request, ServerCallContext context) =>        
            await _client.CreateAsync(request, context.ToCallOptions());

        public override async Task<UpdateContainerResponse> Update(UpdateContainerRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task<Empty> Delete(DeleteContainerRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());
    }
}

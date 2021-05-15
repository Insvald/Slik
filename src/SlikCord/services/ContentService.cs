using Containerd.Services.Content.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class ContentService : Content.ContentBase
    {
        private readonly Content.ContentClient _client;

        public ContentService(Content.ContentClient client)
        {
            _client = client;
        }

        public override async Task<InfoResponse> Info(InfoRequest request, ServerCallContext context) =>
            await _client.InfoAsync(request, context.ToCallOptions());

        public override async Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task List(ListContentRequest request, IServerStreamWriter<ListContentResponse> responseStream, ServerCallContext context) =>
            await GrpcStreaming.ClientAsync(request, responseStream, _client.List, context);
       
        public override async Task<Empty> Delete(DeleteContentRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());

        public override async Task Read(ReadContentRequest request, IServerStreamWriter<ReadContentResponse> responseStream, ServerCallContext context) =>        
            await GrpcStreaming.ClientAsync(request, responseStream, _client.Read, context).ConfigureAwait(false);        

        public override async Task<StatusResponse> Status(StatusRequest request, ServerCallContext context) =>
            await _client.StatusAsync(request, context.ToCallOptions());

        public override async Task<ListStatusesResponse> ListStatuses(ListStatusesRequest request, ServerCallContext context) =>
            await _client.ListStatusesAsync(request, context.ToCallOptions());

        public override async Task Write(IAsyncStreamReader<WriteContentRequest> requestStream, IServerStreamWriter<WriteContentResponse> responseStream, ServerCallContext context) =>
            await GrpcStreaming.BiDirectionalAsync(requestStream, responseStream, _client.Write, context).ConfigureAwait(false);

        public override async Task<Empty> Abort(AbortRequest request, ServerCallContext context) =>
            await _client.AbortAsync(request, context.ToCallOptions());
    }
}

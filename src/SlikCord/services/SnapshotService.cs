using Containerd.Services.Snapshots.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class SnapshotService : Snapshots.SnapshotsBase
    {
        private readonly Snapshots.SnapshotsClient _client;

        public SnapshotService(Snapshots.SnapshotsClient client)
        {
            _client = client;
        }

        public override async Task<Empty> Cleanup(CleanupRequest request, ServerCallContext context) =>
            await _client.CleanupAsync(request, context.ToCallOptions());

        public override async Task<Empty> Commit(CommitSnapshotRequest request, ServerCallContext context) =>
            await _client.CommitAsync(request, context.ToCallOptions());

        public override async Task List(ListSnapshotsRequest request, IServerStreamWriter<ListSnapshotsResponse> responseStream, ServerCallContext context) =>
            await GrpcStreaming.ClientAsync(request, responseStream, _client.List, context).ConfigureAwait(false);
        
        public override async Task<MountsResponse> Mounts(MountsRequest request, ServerCallContext context) =>
            await _client.MountsAsync(request, context.ToCallOptions());

        public override async Task<PrepareSnapshotResponse> Prepare(PrepareSnapshotRequest request, ServerCallContext context) =>
            await _client.PrepareAsync(request, context.ToCallOptions());

        public override async Task<Empty> Remove(RemoveSnapshotRequest request, ServerCallContext context) =>
            await _client.RemoveAsync(request, context.ToCallOptions());

        public override async Task<StatSnapshotResponse> Stat(StatSnapshotRequest request, ServerCallContext context) =>
            await _client.StatAsync(request, context.ToCallOptions());

        public override async Task<UpdateSnapshotResponse> Update(UpdateSnapshotRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task<UsageResponse> Usage(UsageRequest request, ServerCallContext context) =>
            await _client.UsageAsync(request, context.ToCallOptions());

        public override async Task<ViewSnapshotResponse> View(ViewSnapshotRequest request, ServerCallContext context) =>
            await _client.ViewAsync(request, context.ToCallOptions());
    }
}

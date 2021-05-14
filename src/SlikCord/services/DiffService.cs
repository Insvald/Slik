using Containerd.Services.Diff.V1;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class DiffService : Diff.DiffBase
    {
        private readonly Diff.DiffClient _client;

        public DiffService(Diff.DiffClient client)
        {
            _client = client;
        }

        public override async Task<DiffResponse> Diff(DiffRequest request, ServerCallContext context) =>
            await _client.DiffAsync(request, context.ToCallOptions());

        public override async Task<ApplyResponse> Apply(ApplyRequest request, ServerCallContext context) =>
            await _client.ApplyAsync(request, context.ToCallOptions());
    }
}

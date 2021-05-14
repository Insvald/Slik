using Containerd.Services.Tasks.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class TaskService : Tasks.TasksBase
    {
        private readonly Tasks.TasksClient _client;

        public TaskService(Tasks.TasksClient client)
        {
            _client = client;
        }

        public override async Task<CreateTaskResponse> Create(CreateTaskRequest request, ServerCallContext context) =>
            await _client.CreateAsync(request, context.ToCallOptions());

        public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context) =>
            await _client.StartAsync(request, context.ToCallOptions());

        public override async Task<DeleteResponse> Delete(DeleteTaskRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());

        public override async Task<DeleteResponse> DeleteProcess(DeleteProcessRequest request, ServerCallContext context) =>
            await _client.DeleteProcessAsync(request, context.ToCallOptions());

        public override async Task<GetResponse> Get(GetRequest request, ServerCallContext context) =>
            await _client.GetAsync(request, context.ToCallOptions());

        public override async Task<ListTasksResponse> List(ListTasksRequest request, ServerCallContext context) =>
            await _client.ListAsync(request, context.ToCallOptions());

        public override async Task<Empty> Kill(KillRequest request, ServerCallContext context) =>
            await _client.KillAsync(request, context.ToCallOptions());

        public override async Task<Empty> Exec(ExecProcessRequest request, ServerCallContext context) =>
            await _client.ExecAsync(request, context.ToCallOptions());

        public override async Task<Empty> ResizePty(ResizePtyRequest request, ServerCallContext context) =>
            await _client.ResizePtyAsync(request, context.ToCallOptions());

        public override async Task<Empty> CloseIO(CloseIORequest request, ServerCallContext context) =>
            await _client.CloseIOAsync(request, context.ToCallOptions());

        public override async Task<Empty> Pause(PauseTaskRequest request, ServerCallContext context) =>
            await _client.PauseAsync(request, context.ToCallOptions());

        public override async Task<Empty> Resume(ResumeTaskRequest request, ServerCallContext context) =>
            await _client.ResumeAsync(request, context.ToCallOptions());

        public override async Task<ListPidsResponse> ListPids(ListPidsRequest request, ServerCallContext context) =>
            await _client.ListPidsAsync(request, context.ToCallOptions());

        public override async Task<CheckpointTaskResponse> Checkpoint(CheckpointTaskRequest request, ServerCallContext context) =>
            await _client.CheckpointAsync(request, context.ToCallOptions());

        public override async Task<Empty> Update(UpdateTaskRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task<MetricsResponse> Metrics(MetricsRequest request, ServerCallContext context) =>
            await _client.MetricsAsync(request, context.ToCallOptions());

        public override async Task<WaitResponse> Wait(WaitRequest request, ServerCallContext context) =>
            await _client.WaitAsync(request, context.ToCallOptions());
    }
}

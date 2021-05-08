using Grpc.Core;

namespace Slik.Cord
{
    public static class ServerCallContextExtensions
    {
        public static CallOptions ToCallOptions(this ServerCallContext context) =>
            new(context.RequestHeaders, context.Deadline, context.CancellationToken, context.WriteOptions);
    }
}

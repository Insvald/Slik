using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Slik.Cache.Grpc.V1
{
    public class BaseGrpcService
    {
        protected readonly ILogger<BaseGrpcService> Logger;

        public BaseGrpcService(ILogger<BaseGrpcService> logger)
        {
            Logger = logger;
        }

        protected void LogCallEntrance([CallerMemberName] string caller = "")
        {
            Logger.LogDebug($"Received gRPC call: {GetType().Name}.{caller}");
        }

        protected void LogCallExit([CallerMemberName] string caller = "")
        {
            Logger.LogDebug($"Finished gRPC call: {GetType().Name}.{caller}");
        }
    }
}

using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc.V1
{
    public class SlikMembershipGrpcService : BaseGrpcService, ISlikMembershipGrpcService
    {
        private readonly ISlikMembership _slikMembership;

        public SlikMembershipGrpcService(ILogger<SlikMembershipGrpcService> logger, ISlikMembership slikMembership) : base(logger)
        {
            _slikMembership = slikMembership;
        }

        public async Task Add(MemberRequest request)
        {
            LogCallEntrance();
            await _slikMembership.Add(request.Member, CancellationToken.None);
            LogCallExit();
        }

        public async Task Remove(MemberRequest request)
        {
            LogCallEntrance();
            await _slikMembership.Remove(request.Member, CancellationToken.None);
            LogCallExit();
        }   
    }
}
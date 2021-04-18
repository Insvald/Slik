using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Slik.Cache.Grpc.V1
{
    [ServiceContract(Name = "SlikMembership")]
    public interface ISlikMembershipGrpcService
    {
        Task Add(MemberRequest request);
        Task Remove(MemberRequest request);
    }

    [DataContract]
    public class MemberRequest
    {
        [DataMember(Order = 1)]
        public string Member { get; set; } = "";
    }
}

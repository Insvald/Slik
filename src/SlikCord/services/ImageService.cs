using Containerd.Services.Images.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading.Tasks;

namespace Slik.Cord.Services
{
    public class ImageService : Images.ImagesBase
    {
        private readonly Images.ImagesClient _client;

        public ImageService(Images.ImagesClient client)
        {
            _client = client;
        }

        public override async Task<GetImageResponse> Get(GetImageRequest request, ServerCallContext context) =>
            await _client.GetAsync(request, context.ToCallOptions());

        public override async Task<ListImagesResponse> List(ListImagesRequest request, ServerCallContext context) =>
            await _client.ListAsync(request, context.ToCallOptions());

        public override async Task<CreateImageResponse> Create(CreateImageRequest request, ServerCallContext context) =>
            await _client.CreateAsync(request, context.ToCallOptions());

        public override async Task<UpdateImageResponse> Update(UpdateImageRequest request, ServerCallContext context) =>
            await _client.UpdateAsync(request, context.ToCallOptions());

        public override async Task<Empty> Delete(DeleteImageRequest request, ServerCallContext context) =>
            await _client.DeleteAsync(request, context.ToCallOptions());
    }
}

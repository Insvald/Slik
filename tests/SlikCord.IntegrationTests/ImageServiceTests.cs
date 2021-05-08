using Containerd.Services.Images.V1;
using Containerd.Types;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class ImageServiceTests : ServiceTestsBase
    {
        private readonly Images.ImagesClient _client = new(Channel);
        private readonly Image _testImage;
        private const string TestImageName = "hello-world";

        public ImageServiceTests()
        {
            var image = new Image
            {
                Name = TestImageName,
                Target = new Descriptor
                {
                    MediaType = "application/vnd.docker.distribution.manifest.list.v2+json",
                    Digest = "sha256:f2266cbfc127c960fd30e76b7c792dc23b588c0db76233517e1891a4e357d519",
                    Size = 2101
                }
            };

            try
            {
                var response = _client.Create(new CreateImageRequest { Image = image }, Headers);
                _testImage = response.Image;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.AlreadyExists)
            {
                _testImage = image;
            }
        }

        [TestCleanup]
        public async Task DeleteTestImage()
        {
            try
            {
                var request = new DeleteImageRequest { Name = TestImageName };
                await _client.DeleteAsync(request, Headers);
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound) { /* already deleted */}
        }

        [TestMethod]
        public void Create_NewImage_ReturnsImageWithSameName()
        {
            Assert.AreEqual(TestImageName, _testImage.Name);
        }

        [TestMethod]
        public async Task Update_ExistingImage_AddsLabels()
        {
            string testKey = "test-key";
            string testValue = "test-value";

            var request = new UpdateImageRequest
            {
                Image = new Image { Name = TestImageName },
                UpdateMask = new FieldMask()
            };

            request.Image.Labels.Add(testKey, testValue);
            request.UpdateMask.Paths.Add("labels");

            var response = await _client.UpdateAsync(request, Headers);
            Assert.IsTrue(response.Image.Labels[testKey] == testValue);
        }

        [TestMethod]
        public async Task Delete_ExistingImage_MakesGetThrowException()
        {
            await DeleteTestImage();

            var request = new GetImageRequest { Name = TestImageName };

            try
            {
                await _client.GetAsync(request, Headers);
                Assert.Fail();
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                // OK
            }
        }

        [TestMethod]
        public async Task Get_ExistingImage_ReturnsImageWithSameName()
        {
            var request = new GetImageRequest { Name = TestImageName };

            var response = await _client.GetAsync(request, Headers);

            Assert.AreEqual(TestImageName, response.Image.Name);
        }

        [TestMethod]
        public async Task List_ExistingImage_ReturnsListWithExistingImage()
        {
            var response = await _client.ListAsync(new ListImagesRequest(), Headers);
            bool exists = response.Images.Any(i => i.Name == TestImageName);

            Assert.IsTrue(exists);
        }
    }
}

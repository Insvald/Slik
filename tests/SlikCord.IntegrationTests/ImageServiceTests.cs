using Containerd.Services.Images.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
    public class ImageServiceTests : ServiceTestsBase
    {
        [TestMethod]
        public async Task Create_ReturnsCorrectImageName()
        {
            var client = new Images.ImagesClient(Fixture.Channel);

            var image = new Image
            {
                Name = "hello-world"
            };

            var response = await client.CreateAsync(new CreateImageRequest { Image = image }, Fixture.Headers);

            Assert.AreEqual(image.Name, response.Image.Name);
        }

        [TestMethod]
        public async Task List_ReturnsList()
        {
            var client = new Images.ImagesClient(Fixture.Channel);

            var response = await client.ListAsync(new ListImagesRequest(), Fixture.Headers);
        }
    }
}

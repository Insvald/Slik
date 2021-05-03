using Containerd.Services.Containers.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
    public class ContainerServiceTests : ServiceTestsBase
    {
        [TestMethod]
        public async Task Get_NewlyCreatedContainer_ReturnsById()
        {
            string id = "test-container";

            var client = new Containers.ContainersClient(Fixture.Channel);
            var container = new Container 
            { 
                Id = id, 
                Image = "docker.io/library/redis:alpine", 
                Runtime = new Container.Types.Runtime { Name = "io.containerd.runhcs.v1" }
            };

            var createResponse = await client.CreateAsync(new CreateContainerRequest { Container = container }, Fixture.Headers);

            var getResponse = await client.GetAsync(new GetContainerRequest { Id = id }, Fixture.Headers);
            Assert.IsNotNull(getResponse.Container);
            Assert.AreEqual(id, getResponse.Container.Id);
        }
    }
}

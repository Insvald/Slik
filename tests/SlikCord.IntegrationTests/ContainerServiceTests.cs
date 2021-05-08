using Containerd.Services.Containers.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class ContainerServiceTests : ServiceTestsBase
    {
        private readonly Containers.ContainersClient _client = new(Channel);
        private readonly Container _container;

        private const string TestImageName = "hello-world";
        private const string TestContainerId = "test-container";

        private async Task<Container> CreateContainer(string id)
        {
            var container = new Container
            {
                Id = TestContainerId,
                Image = TestImageName,
                //Runtime = new Container.Types.Runtime { Name = "io.containerd.runhcs.v1" }, // windows
                Runtime = new Container.Types.Runtime { Name = "io.containerd.runc.v1" }, // linux
                Spec = Any.Pack(new Empty())
                // Spec:
                // https://github.com/containerd/containerd/blob/ab963e1cc16a845567a0e3e971775c29c701fcf8/vendor/github.com/opencontainers/runtime-spec/specs-go/config.go#L6
                // https://github.com/opencontainers/runtime-spec/blob/master/schema/test/config/good/minimal-for-start.json             
            };

            try
            {
                var createResponse = await _client.CreateAsync(new CreateContainerRequest { Container = container }, Headers);
                return createResponse.Container;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.AlreadyExists)
            {
                return container;
            }
        }

        public ContainerServiceTests()
        {            
            _container = CreateContainer(TestContainerId).Result;
        }

        [TestCleanup]
        public async Task DeleteTestContainer()
        {
            try
            {
                var request = new DeleteContainerRequest { Id = TestContainerId };
                await _client.DeleteAsync(request, Headers);
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound) { /* already deleted */}
        }

        [TestMethod]
        public void Create_NewContainer_ReturnsTheSameId()
        {
            Assert.AreEqual(TestContainerId, _container.Id);
        }

        [TestMethod]
        public async Task Get_ExistingContainer_ReturnsTheSameId()
        {
            var request = new GetContainerRequest { Id = TestContainerId };
            var response = await _client.GetAsync(request, Headers);
            Assert.AreEqual(TestContainerId, response.Container.Id);
        }

        [TestMethod]
        public async Task Update_ExistingContainer_AddsLabels()
        {
            string testKey = "test-key";
            string testValue = "test-value";

            var request = new UpdateContainerRequest
            {
                Container = new Container  { Id = TestContainerId },
                UpdateMask = new FieldMask()
            };

            request.Container.Labels.Add(testKey, testValue);
            request.UpdateMask.Paths.Add("labels");

            var response = await _client.UpdateAsync(request, Headers);
            Assert.IsTrue(response.Container.Labels[testKey] == testValue);
        }

        [TestMethod]
        public async Task Delete_ExistingContainer_MakesGetThowException()
        {
            await DeleteTestContainer();

            var request = new GetContainerRequest { Id = TestContainerId };

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
        public async Task List_ExistingContainer_ReturnsListWithExistingContainer()
        {
            var response = await _client.ListAsync(new ListContainersRequest(), Headers);
            bool exists = response.Containers.Any(i => i.Id == TestContainerId);
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public async Task ListStream_ExistingContainer_ReturnsListWithExistingContainer()
        {
            using var streamingCall = _client.ListStream(new ListContainersRequest(), Headers);
            List<string> ids = new();

            await foreach (var containerMessage in streamingCall.ResponseStream.ReadAllAsync())
            {
                ids.Add(containerMessage.Container.Id);                
            }

            bool exists = ids.Contains(TestContainerId);
            Assert.IsTrue(exists);
        }
    }
}

using Containerd.Services.Containers.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Slik.Cord.Services;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        private readonly ContainerFactory _factory;
        private readonly Container _container;

        private const string TestImageName = "hello-world";
        private const string TestContainerId = "test-container";        

        public ContainerServiceTests()
        {
            _factory = new ContainerFactory(_client, OSPlatform.Linux, Headers);
            _container = _factory.CreateContainerAsync(TestContainerId, TestImageName).Result;
        }

        [TestCleanup]
        public async Task DeleteTestContainer()
        {
            await _factory.DeleteContainerAsync(_container);
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

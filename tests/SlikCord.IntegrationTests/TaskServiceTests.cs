using Containerd.Services.Containers.V1;
using Containerd.Services.Tasks.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class TaskServiceTests : ServiceTestsBase
    {
        private readonly ContainerFactory _factory;
        private readonly Tasks.TasksClient _client = new(Channel);
        private Container _container;
        private const string TestImageName = "hello-world";
        private const string TestContainerId = "test-container";

        public TaskServiceTests()
        {
            _factory = new ContainerFactory(new Containers.ContainersClient(Channel), OSPlatform.Linux, Headers);
            _container = _factory.CreateContainerAsync(TestContainerId, TestImageName).Result;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _factory.DeleteContainerAsync(_container);
        }

        [TestMethod]
        [Ignore]
        public async Task Create_NewTask_ReturnsNonZeroPid()
        {
            var request = new CreateTaskRequest
            {
                ContainerId = _container.Id
            };

            var response = await _client.CreateAsync(request, Headers);

            try
            {
                Assert.IsTrue(response.Pid != 0);
            }
            finally
            {
                var deleteRequest = new DeleteTaskRequest
                {
                    ContainerId = _container.Id
                };

                await _client.DeleteAsync(deleteRequest, Headers);
            }
        }

        [TestMethod]
        public async Task List_ReturnsZeroOrMoreTasks()
        {
            var response = await _client.ListAsync(new ListTasksRequest { }, Headers);
            Assert.IsTrue(response.Tasks.Count >= 0);            
        }       
    }
}

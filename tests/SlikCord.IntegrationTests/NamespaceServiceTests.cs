using Containerd.Services.Namespaces.V1;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
    public class NamespaceServiceTests : ServiceTestsBase
    {
        private readonly Namespaces.NamespacesClient _client = new(Channel);
        private const string TestNamespace = "test-namespace";
        private const string TestLabelKey = "test-key";
        private const string TestLabelValue1 = "test1";
        private const string TestLabelValue2 = "test2";

        private async Task<Namespace> CreateNamespace()
        {
            var createRequest = new CreateNamespaceRequest
            {
                Namespace = new Namespace
                {
                    Name = TestNamespace
                }
            };

            createRequest.Namespace.Labels.Add(TestLabelKey, TestLabelValue1);

            var response = await _client.CreateAsync(createRequest, Headers);

            return response.Namespace;
        }

        private async Task DeleteNamespace(Namespace testNamespace)
        {
            var deleteRequest = new DeleteNamespaceRequest
            {
                Name = testNamespace.Name
            };

            await _client.DeleteAsync(deleteRequest, Headers);
        }

        private async Task<RepeatedField<Namespace>> ListNamespaces()
        {
            var request = new ListNamespacesRequest
            {
                Filter = $"namespace=={TestNamespace}"
            };

            var response = await _client.ListAsync(request, Headers);
            return response.Namespaces;
        }

        private async Task UseTestNamespace(Func<Namespace, Task> namespaceAction)
        {
            var testNamespace = await CreateNamespace();

            try
            {
                await namespaceAction(testNamespace);
            }
            finally
            {
                await DeleteNamespace(testNamespace);
            }
        }

        [TestMethod]
        public async Task Create_NewNamespace_CreatesNamespaceWithCorrectName()
        {
            await UseTestNamespace(testNamespace =>
            {
                Assert.AreEqual(TestNamespace, testNamespace.Name);
                return Task.CompletedTask;
            });            
        }

        [TestMethod]
        public async Task Delete_ExistingNamespace_DeletesIt()
        {
            var testNamespace = await CreateNamespace();
            await DeleteNamespace(testNamespace);

            var namespaces = await ListNamespaces();
            bool exists = namespaces.Any(n => n.Name == testNamespace.Name);

            Assert.IsFalse(exists);
        }

        [TestMethod]
        public async Task List_ExistingNamespace_ReturnsListWithIt()
        {
            await UseTestNamespace(async testNamespace => 
            {
                var namespaces = await ListNamespaces();
                bool exists = namespaces.Any(n => n.Name == testNamespace.Name);

                Assert.IsTrue(exists);
            });            
        }

        [TestMethod]
        public async Task Get_ExistingNamespace_ReturnsIt()
        {
            await UseTestNamespace(async testNamespace =>
            {
                var request = new GetNamespaceRequest
                {
                    Name = TestNamespace
                };

                var response = await _client.GetAsync(request, Headers);

                Assert.AreEqual(TestNamespace, response.Namespace.Name);
            });
        }

        [TestMethod]
        public async Task Update_ExistingNamespace_AddsLabels()
        {
            await UseTestNamespace(async testNamespace => 
            { 
                var updateRequest = new UpdateNamespaceRequest
                {
                    Namespace = new Namespace
                    {
                        Name = testNamespace.Name
                    },
                    UpdateMask = new FieldMask()
                };

                updateRequest.Namespace.Labels.Add(TestLabelKey, TestLabelValue2);
                updateRequest.UpdateMask.Paths.Add($"labels.{TestLabelKey}");

                await _client.UpdateAsync(updateRequest, Headers); // always returns empty response, need to re-read

                var getRequest = new GetNamespaceRequest
                {
                    Name = testNamespace.Name
                };

                var getResponse = await _client.GetAsync(getRequest, Headers);
                
                Assert.IsTrue(getResponse.Namespace.Labels[TestLabelKey] == TestLabelValue2);
            });
        }
    }
}

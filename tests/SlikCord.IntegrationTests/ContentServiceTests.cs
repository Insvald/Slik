using Containerd.Services.Content.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class ContentServiceTests : ServiceTestsBase
    {
        private readonly Content.ContentClient _client = new(Channel);
        private readonly Random rnd = new();
            
        private async Task<string> WriteTestObject(AsyncDuplexStreamingCall<WriteContentRequest, WriteContentResponse> streamingCall)
        {
            string objectRef = Guid.NewGuid().ToString();

            await streamingCall.RequestStream.WriteAsync(new WriteContentRequest
            {
                Action = WriteAction.Write,
                Data = GetRandomBytes(),
                Ref = objectRef
            });

            await Task.Delay(200); // sometimes we check the write results too fast

            return objectRef;
        }        

        private ByteString GetRandomBytes()
        {
            var bytes = new byte[1024];
            rnd.NextBytes(bytes);
            return ByteString.CopyFrom(bytes);
        }

        private async Task<string> GetTestObject()
        {
            using var streamingCall = _client.Write(Headers);
            string objectRef = await WriteTestObject(streamingCall);

            // commit
            await streamingCall.RequestStream.WriteAsync(new WriteContentRequest
            {
                Action = WriteAction.Commit,
                Data = GetRandomBytes(),
                Ref = objectRef
            });

            await streamingCall.RequestStream.CompleteAsync();

            string digest = "";

            await foreach (var response in streamingCall.ResponseStream.ReadAllAsync())
            {
                digest = response.Digest;
            }

            return digest;
        }

        private async Task UsingTestObject(Func<string, Task> usageAction)
        {            
            string digest = await GetTestObject();

            try
            {
                await usageAction(digest);
            }
            finally
            {
                await _client.DeleteAsync(new DeleteContentRequest { Digest = digest }, Headers);
            }
        }

        [TestMethod]
        public async Task List_ExistingObject_ReturnsNonEmptyInfo()
        {
            await UsingTestObject(async digest =>
            {
                using var streamingCall = _client.List(new ListContentRequest(), Headers);
                List<Info> infoList = new();

                await foreach (var contentResponse in streamingCall.ResponseStream.ReadAllAsync())
                {
                    infoList.AddRange(contentResponse.Info);
                }

                Assert.IsTrue(infoList.Count > 0);
            });
        }

        [TestMethod]
        public async Task Write_NewObject_ReturnsNonEmptyDigest()
        {
            await UsingTestObject(digest =>
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(digest));
                return Task.CompletedTask;
            });
        }        

        [TestMethod]
        public async Task Delete_ExistingObject_DeletesIt()
        {
            string digest = await GetTestObject();

            await _client.DeleteAsync(new DeleteContentRequest { Digest = digest }, Headers);

            try
            {
                await _client.InfoAsync(new InfoRequest { Digest = digest }, Headers);
                Assert.Fail();
            }
            catch(RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                // OK
            }            
        }

        [TestMethod]
        public async Task Info_ExistingObject_ReturnsNonNullInfo()
        {
            await UsingTestObject(async digest =>
            {
                var response = await _client.InfoAsync(new InfoRequest { Digest = digest }, Headers);
                Assert.IsNotNull(response);
            });
        }

        [TestMethod]
        public async Task Update_ExistingObject_AddsLabels()
        {
            await UsingTestObject(async digest =>
            {
                string testKey = "test-key";
                string testValue = "test-value";

                var request = new UpdateRequest
                {
                    Info = new Info { Digest = digest },
                    UpdateMask = new FieldMask()
                };

                request.Info.Labels.Add(testKey, testValue);
                request.UpdateMask.Paths.Add("labels");

                var response = await _client.UpdateAsync(request, Headers);
                Assert.IsTrue(response.Info.Labels[testKey] == testValue);
            });
        }

        [TestMethod]
        public async Task Read_ExistingObject_ReturnsIt()
        {
            await UsingTestObject(async digest =>
            {
                using var streamingCall = _client.Read(new ReadContentRequest { Digest = digest }, Headers);

                List<ByteString> data = new();
                await foreach (var response in streamingCall.ResponseStream.ReadAllAsync())
                {
                    data.Add(response.Data);
                }

                Assert.IsTrue(data.Count > 0);
            });
        }

        [TestMethod]
        public async Task Status_AfterWrite_ReturnsStatusWithTheSameRef()
        {
            using var streamingCall = _client.Write(Headers);
            string objectRef = await WriteTestObject(streamingCall);

            try
            {
                var response = await _client.StatusAsync(new StatusRequest { Ref = objectRef }, Headers);
                Assert.AreEqual(objectRef, response.Status.Ref);
            }
            finally
            {
                await _client.AbortAsync(new AbortRequest { Ref = objectRef }, Headers);
            }
        }

        [TestMethod]
        public async Task ListStatus_AfterWrite_ReturnsNonEmptyList()
        {
            using var streamingCall = _client.Write(Headers);
            string objectRef = await WriteTestObject(streamingCall);
            try
            {
                var response = await _client.ListStatusesAsync(new ListStatusesRequest(), Headers);
                Assert.IsTrue(response.Statuses.Count > 0);
            }
            finally
            {
                await _client.AbortAsync(new AbortRequest { Ref = objectRef }, Headers);
            }
        }

        public async Task Abort_AfterWrite_ClearsPendingWrites()
        {
            using var streamingCall = _client.Write(Headers);
            string objectRef = await WriteTestObject(streamingCall);
            await _client.AbortAsync(new AbortRequest { Ref = objectRef }, Headers);

            try
            {
                var response = await _client.StatusAsync(new StatusRequest { Ref = objectRef }, Headers);
                Assert.Fail();
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
            {
                // OK
            }
        }
    }
}

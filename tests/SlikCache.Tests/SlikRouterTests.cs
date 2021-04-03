using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class SlikRouterTests
    {
        private readonly SlikCache _cache = SlikCacheHelper.InitCache();
        private readonly SlikRouter _router;
        private readonly Mock<IMessageBus> _messageBusMock = new ();

        public SlikRouterTests()
        {
            var clusterMock = new Mock<IRaftCluster>();            
            var loggerMock = new Mock<ILogger<SlikRouter>>();

            _router = new SlikRouter(_cache, clusterMock.Object, _messageBusMock.Object, loggerMock.Object);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            SlikCacheHelper.DestroyCache(_cache);
        }

        [TestMethod]
        public async Task LookForLeaderAsync_NoLeader_ThrowsException()
        {
            _messageBusMock.SetupGet(m => m.Leader).Returns((ISubscriber?)null);
            _router.LeaderWaitTimeout = TimeSpan.FromSeconds(0.3); // to shorten the test time
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => _router.LookForLeaderAsync(CancellationToken.None));
        }

        [TestMethod]
        public async Task LookForLeaderAsync_LeaderAppearsBeforeTimeout_ReturnsIt()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.5));
            _router.LeaderWaitTimeout = TimeSpan.FromSeconds(1);
            var leaderMock = new Mock<ISubscriber>().Object;

            _messageBusMock.SetupGet(m => m.Leader).Returns(() => 
            {
                // return a leader after 500 ms
                return cts.IsCancellationRequested ? leaderMock : null;
            });

            var leader = await _router.LookForLeaderAsync(CancellationToken.None);
            Assert.AreEqual(leaderMock, leader);
        }        

        private void ArrangeLeader(bool isRemote, string response = "", Action? sendMessageCallback = null)
        {
            var leaderMock = new Mock<ISubscriber>();
            leaderMock.SetupGet(l => l.IsRemote).Returns(isRemote);

            var outputChannelMock = new Mock<IOutputChannel>();
            outputChannelMock
                .Setup(o => o.SendMessageAsync(It.IsAny<IMessage>(), It.IsAny<MessageReader<string>>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(response))
                .Callback(sendMessageCallback ?? (() => { }));

            _messageBusMock.SetupGet(m => m.Leader).Returns(leaderMock.Object);
            _messageBusMock.SetupGet(m => m.LeaderRouter).Returns(outputChannelMock.Object);
        }

        [TestMethod]
        public async Task UpdateLeaderAsync_LocalLeader_ReturnsFalse()
        {            
            ArrangeLeader(false);
            bool handled = await _router.UpdateLeaderAsync(new CacheLogRecord(CacheOperation.Update, "key", Array.Empty<byte>()));
            Assert.IsFalse(handled);
        }

        [TestMethod]
        public async Task UpdateLeaderAsync_RemoteLeader_ReturnsTrue()
        {                     
            ArrangeLeader(true, SlikRouter.OK);
            bool handled = await _router.UpdateLeaderAsync(new CacheLogRecord(CacheOperation.Update, "key", Array.Empty<byte>()));
            Assert.IsTrue(handled);
        }

        [TestMethod]
        public async Task UpdateLeaderAsync_RemoteLeaderDoesNotConfirm_ThrowsException()
        {
            ArrangeLeader(true, "Not OK");
            await Assert.ThrowsExceptionAsync<SlikRouter.RouterException>(() => 
                _router.UpdateLeaderAsync(new CacheLogRecord(CacheOperation.Update, "key", Array.Empty<byte>())).AsTask());            
        }

        private async Task<string> ArrangeMessageAndReadResponse(IMessage message)
        {
            var subscriberMock = new Mock<ISubscriber>();
            var response = await _router.ReceiveMessage(subscriberMock.Object, message, null, CancellationToken.None);
            return await response.ReadAsTextAsync();
        }

        [TestMethod]
        public async Task ReceiveMessage_IncorrectMessageName_ReturnsNotOK()
        {            
            var messageMock = new Mock<IMessage>();
            messageMock.SetupGet(m => m.Name).Returns("incorrect message name");

            string response = await ArrangeMessageAndReadResponse(messageMock.Object);

            Assert.AreNotEqual(SlikRouter.OK, response);
        }

        [TestMethod]
        public async Task ReceiveMessage_IncorrectMessageType_ReturnsNotOK()
        {
            var message = new TextMessage("hello", SlikRouter.RequestMessageName);

            string response = await ArrangeMessageAndReadResponse(message);

            Assert.AreNotEqual(SlikRouter.OK, response);
        }

        [TestMethod]
        public async Task ReceiveMessage_EmptyJsonMessage_ReturnsNotOK()
        {
#pragma warning disable 8625 // sending null on purpose
            var message = new JsonMessage<CacheLogRecord>(SlikRouter.RequestMessageName, null);
#pragma warning restore 8625

            string response = await ArrangeMessageAndReadResponse(message);

            Assert.AreNotEqual(SlikRouter.OK, response);
        }

        [TestMethod]
        public async Task ReceiveMessage_CorrectMessageAndLocalLeader_ReturnsOKAfterProcessingLocally()
        {
            bool relayed = false;
            ArrangeLeader(false, SlikRouter.OK, () => relayed = true);

            var message = new JsonMessage<CacheLogRecord>(SlikRouter.RequestMessageName, new CacheLogRecord(CacheOperation.Update, "key", new byte[] { 1, 2, 3 }));

            string response = await ArrangeMessageAndReadResponse(message);

            Assert.AreEqual(SlikRouter.OK, response);
            Assert.IsFalse(relayed);
        }

        [TestMethod]
        public async Task ReceiveMessage_CorrectMessageAndLocalLeader_ReturnsOKAfterRelayingRemotely()
        {
            bool relayed = false;
            ArrangeLeader(true, SlikRouter.OK, () => relayed = true);

            var message = new JsonMessage<CacheLogRecord>(SlikRouter.RequestMessageName, new CacheLogRecord(CacheOperation.Update, "key", Array.Empty<byte>()));

            string response = await ArrangeMessageAndReadResponse(message);

            Assert.AreEqual(SlikRouter.OK, response);
            Assert.IsTrue(relayed);
        }
    }
}

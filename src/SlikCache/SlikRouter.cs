using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache
{
    /// <summary>
    /// Redirects updates to a leader and handles such requests from followers
    /// </summary>
    internal class SlikRouter : IHostedService, IInputChannel
    {
        private readonly ILogger<SlikRouter> _logger;
        private readonly SlikCache _slikCache;
        private readonly SlikMembershipHandler? _slikMembership;
        private readonly IMessageBus _messageBus;
        private readonly IRaftCluster _cluster;

        internal const string CacheRequestMessage = "slik-cache-request";
        internal const string CacheResponseMessage = "slik-cache-response";
        internal const string MemberRequestMessage = "slik-member-request";
        internal const string MemberResponseMessage = "slik-member-response";
        internal const string OK = "OK";
        private const string WrongRequest = "Wrong request";

        public SlikRouter(SlikCache slikCache, IServiceProvider serviceProvider, IRaftCluster cluster, IMessageBus messageBus, ILogger<SlikRouter> logger)
        {
            _logger = logger;
            _slikCache = slikCache;
            _slikMembership = serviceProvider.GetService<SlikMembershipHandler>();
            _messageBus = messageBus;
            _cluster = cluster;
        }

        public TimeSpan LeaderWaitTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan LeaderCheckTimeout { get; set; } = TimeSpan.FromSeconds(0.1);

        private async ValueTask<string> MessageReader(IMessage message, CancellationToken token)
        {
            if (message.Name != CacheResponseMessage && message.Name != MemberResponseMessage)
                _logger.LogWarning($"Wrong response received: '{message.Name}'");

            return await message.ReadAsTextAsync(token).ConfigureAwait(false);
        }

        public async Task<ISubscriber> LookForLeaderAsync(CancellationToken token)
        {
            // creating linked tokens
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(LeaderWaitTimeout);

            var leader = _messageBus.Leader;

            while (leader == null && !cts.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting for leader election");
                    await Task.Delay(LeaderCheckTimeout, cts.Token).ConfigureAwait(false);
                    leader = _messageBus.Leader;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogDebug($"Found leader '{leader?.EndPoint}'");

            return leader ?? throw new TimeoutException("A leader has not been elected, impossible to update cache");
        }

        public class RouterException : Exception
        {
            public RouterException(string message) : base(message) { }
        }

        private async ValueTask<bool> UpdateLeaderAsync<T>(T record, string messageName, CancellationToken token = default)
        {
            var leader = await LookForLeaderAsync(token).ConfigureAwait(false);

            if (leader.IsRemote)
            {
                _logger.LogDebug($"Relaying to the leader, apparently {leader.EndPoint}");
                string response = await _messageBus.LeaderRouter
                    .SendMessageAsync(new JsonMessage<T>(messageName, record), MessageReader, token)
                    .ConfigureAwait(false);

                if (response == OK)
                    _logger.LogDebug("Confirmation from the leader received");
                else
                    throw new RouterException($"The leader didn't confirm the remote change: '{response}'");

                return true;
            }

            _logger.LogDebug("The current node is the leader, handling locally");
            return false;
        }

        internal async ValueTask<bool> CacheUpdateLeaderAsync(CacheLogRecord record, CancellationToken token) =>
            await UpdateLeaderAsync(record, CacheRequestMessage, token).ConfigureAwait(false);

        private async ValueTask<bool> MemberUpdateLeaderAsync(MembershipChangeRecord record, CancellationToken token) =>
            await UpdateLeaderAsync(record, MemberRequestMessage, token).ConfigureAwait(false);

        private async Task ApplyCacheRecordLocally(CacheLogRecord record, CancellationToken token)
        {
            switch (record.Operation)
            {
                case CacheOperation.Update:
                    _logger.LogDebug("Applying a cache change from remote node");
                    await _slikCache.SetAsync(record.Key, record.Value, record.Options ?? new(), token).ConfigureAwait(false);
                    break;
                case CacheOperation.Remove:
                    _logger.LogDebug("Applying a cache removal from remote node");
                    await _slikCache.RemoveAsync(record.Key, token).ConfigureAwait(false);
                    break;
                case CacheOperation.Refresh:
                    _logger.LogDebug("Applying a cache refresh from remote node");
                    await _slikCache.RefreshAsync(record.Key, token).ConfigureAwait(false);
                    break;
            }
        }

        private async Task ApplyMembershipRecordLocally(MembershipChangeRecord record, CancellationToken token)
        {
            if (_slikMembership != null)
            {
                switch (record.Operation)
                {
                    case MembershipChangeRecord.MemebershipOperation.Add:
                        _logger.LogDebug("Applying a member addition from remote node");
                        await _slikMembership.Add(record.Member, token).ConfigureAwait(false);
                        break;
                    case MembershipChangeRecord.MemebershipOperation.Remove:
                        _logger.LogDebug("Applying a member removal from remote node");
                        await _slikMembership.Remove(record.Member, token).ConfigureAwait(false);
                        break;
                }
            }
        }

        public async Task<bool> ReplicateAsync(TimeSpan timeout, CancellationToken token) =>
            await _cluster.ForceReplicationAsync(timeout, token).ConfigureAwait(false);

        #region IInputChannel implementation
        public async Task<IMessage> ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token)
        {
            string response = OK;

            try
            {
                switch (message.Name)
                {
                    case CacheRequestMessage:
                        var cacheRecord = await JsonMessage<CacheLogRecord>.FromJsonAsync(message, token).ConfigureAwait(false)
                            ?? throw new Exception($"empty message");

                        // check if the current node is the leader
                        if (_messageBus.Leader?.IsRemote == false)
                        {
                            await ApplyCacheRecordLocally(cacheRecord, token).ConfigureAwait(false);
                        }
                        else
                        {
                            // try to relay to a new leader
                            bool handled = await CacheUpdateLeaderAsync(cacheRecord, token).ConfigureAwait(false);
                            if (!handled) // ping pong, we will take care of it then
                            {
                                await ApplyCacheRecordLocally(cacheRecord, token).ConfigureAwait(false);
                            }
                        }
                        break;
                    case MemberRequestMessage:
                        var membershipRecord = await JsonMessage<MembershipChangeRecord>.FromJsonAsync(message, token).ConfigureAwait(false)
                                ?? throw new Exception($"empty message");

                        // check if the current node is the leader
                        if (_messageBus.Leader?.IsRemote == false)
                        {
                            await ApplyMembershipRecordLocally(membershipRecord, token).ConfigureAwait(false);
                        }
                        else
                        {
                            // try to relay to a new leader
                            bool handled = await MemberUpdateLeaderAsync(membershipRecord, token).ConfigureAwait(false);
                            if (!handled) // ping pong, we will take care of it then
                            {
                                await ApplyMembershipRecordLocally(membershipRecord, token).ConfigureAwait(false);
                            }
                        }
                        break;
                    default:
                        response = $"{WrongRequest}: name = '{message.Name}', length = {message.Length?.ToString() ?? "undetermined"}";
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error while processing the request");
                response = $"Unexpected error while processing the request: {e.Message}";
            }


            _logger.LogDebug($"Response sent: '{response}'");
            return new TextMessage(response, message.Name == CacheRequestMessage ? CacheResponseMessage : MemberResponseMessage);
        }

        public Task ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token) => throw new NotImplementedException();
        #endregion

        #region IHostedService implementation
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _slikCache.RedirectHandler += CacheUpdateLeaderAsync;
            _slikCache.ReplicateHandler += ReplicateAsync;

            if (_slikMembership != null)
                _slikMembership.RedirectHandler += MemberUpdateLeaderAsync;

            _messageBus.AddListener(this);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _slikCache.RedirectHandler -= CacheUpdateLeaderAsync;
            _slikCache.ReplicateHandler -= ReplicateAsync;

            if (_slikMembership != null)
            {
                string localEndpoint = $"https://{_cluster.Members.FirstOrDefault(m => !m.IsRemote)?.EndPoint}";

                await _slikMembership.Remove(localEndpoint, cancellationToken).ConfigureAwait(false);

                _slikMembership.RedirectHandler -= MemberUpdateLeaderAsync;                
            }

            _messageBus.RemoveListener(this);
        }
        #endregion
    }
}
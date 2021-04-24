using DotNext.Net.Cluster;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProtoBuf.Grpc.Client;
using Slik.Cache.Grpc.V1;
using Slik.Security;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache
{
    public interface ISlikMembership
    {
        Task Add(string member, CancellationToken token);
        Task Remove(string member, CancellationToken token);
    }

    public class MembershipChangeRecord
    {
        public enum MemebershipOperation { Add, Remove };

        public MemebershipOperation Operation { get; set; }
        public string Member { get; set; } = "";
    }

    internal class SlikMembershipHandler : ISlikMembership
    {
        private readonly ILogger<SlikMembershipHandler> _logger;
        private readonly IConfiguration _config;
        private readonly IOptionsMonitor<SlikOptions> _options;
        private readonly IExpandableCluster _cluster;
        private readonly object _lock = new();

        public SlikMembershipHandler(ILogger<SlikMembershipHandler> logger, IConfiguration config, 
            IHttpMessageHandlerFactory httpHandlerFactory, IOptionsMonitor<SlikOptions> options, IExpandableCluster cluster)
        {
            _logger = logger;
            _config = config;
            _options = options;

            _cluster = cluster;
            _cluster.MemberAdded += (_, member) => _logger.LogInformation($"Cluster member '{member.EndPoint}' has been added");
            _cluster.MemberRemoved += (_, member) => _logger.LogInformation($"Cluster member '{member.EndPoint}' has been removed");

            _ = UpdateClusterMembershipAsync(httpHandlerFactory);
        }

        private async Task UpdateClusterMembershipAsync(IHttpMessageHandlerFactory httpHandlerFactory)
        {
            try
            {
                var localIps = NetworkUtils
                    .GetLocalIPAddresses()
                    .Select(ip => ip.ToString())
                    .Union(new[] { "127.0.0.1", "localhost" });

                var remoteMembers = _options.CurrentValue.Members.Where(member => 
                {
                    var uri = new Uri(member);
                    return uri.Port != _options.CurrentValue.Host.Port || !localIps.Contains(uri.Host);
                });

                if (remoteMembers.Any())
                {
                    using var httpHandler = httpHandlerFactory.CreateHandler();

                    bool success = false;
                    for (int i = 0; i < 3; i++)
                    {
                        foreach (var member in remoteMembers)
                        {
                            _logger.LogDebug($"Trying to contact member '{member}' for adding this node.");
                            try
                            {
                                using var channel = GrpcChannel.ForAddress(member, new GrpcChannelOptions { HttpHandler = httpHandler });
                                var service = channel.CreateGrpcService<ISlikMembershipGrpcService>();
                                await service.Add(new MemberRequest { Member = $"https://{_options.CurrentValue.Host}" }).ConfigureAwait(false);
                                success = true;
                            }
                            catch (Exception e)
                            {
                                _logger.LogWarning(e, $"Error contacting remote member '{member}'.");
                            }
                        }

                        if (success)
                            break;
                        else
                            await Task.Delay(300).ConfigureAwait(false);
                    }
                }
                else
                    _logger.LogDebug("No remote members found, this node is alone.");
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Error while trying to get added to a cluster");
            }
        }

        private void ReloadConfig()
        {
            _logger.LogDebug("Trying to reload the configuration");
            if (_config is IConfigurationRoot configRoot)
            {
                configRoot.Reload();
            }
            else
            {
                _logger.LogError("Error reloading configuration");
            }
        }

        private async Task ChangeMembershipAsync(MembershipChangeRecord record, CancellationToken token)
        {
            bool handled = RedirectHandler != null && await RedirectHandler(record, token).ConfigureAwait(false);

            if (!handled)
            {
                _logger.LogDebug("The change is not handled by the router, applying locally");

                lock (_lock)
                {
                    var members = _cluster.Members.Select(m => m.EndPoint.ToString());
                    int memberCount = members.Count();

                    switch (record.Operation)
                    {
                        case MembershipChangeRecord.MemebershipOperation.Add:
                            if (!members.Contains(record.Member))
                            {
                                _config[$"members:{memberCount}"] = record.Member;
                                _logger.LogDebug($"Member '{record.Member}' has been added to the configuration");
                                ReloadConfig();
                            }
                            else
                                _logger.LogDebug($"Member '{record.Member}' has been added already");
                                                        
                            break;

                        case MembershipChangeRecord.MemebershipOperation.Remove:
                            if (members.Contains(record.Member))
                            {
                                // remove the last one
                                string memberIndex = $"members:{memberCount - 1}";
                                string removed = _config[memberIndex];
                                _config[memberIndex] = null;

                                // replace the correct one with the removed
                                if (!record.Member.Equals(removed, StringComparison.OrdinalIgnoreCase))
                                {
                                    for (int i = 0; i < memberCount - 1; i++)
                                    {
                                        memberIndex = $"members:{i}";
                                        if (record.Member.Equals(_config[memberIndex], StringComparison.OrdinalIgnoreCase))
                                        {
                                            _config[memberIndex] = removed;
                                            break;
                                        }
                                    }
                                }

                                ReloadConfig();
                            }
                            else
                                _logger.LogDebug($"Member '{record.Member}' has been removed already or didn't exist");

                            break;
                    }
                }
            }
        }

        public async Task Add(string member, CancellationToken token)
        {
            var record = new MembershipChangeRecord { Member = member, Operation = MembershipChangeRecord.MemebershipOperation.Add };

            await ChangeMembershipAsync(record, token).ConfigureAwait(false);
        }

        public async Task Remove(string member, CancellationToken token)
        {
            var record = new MembershipChangeRecord { Member = member, Operation = MembershipChangeRecord.MemebershipOperation.Remove };

            await ChangeMembershipAsync(record, token).ConfigureAwait(false);
        }

        // delegate to handle leader redirection
        internal event Func<MembershipChangeRecord, CancellationToken, ValueTask<bool>>? RedirectHandler;
    }
}

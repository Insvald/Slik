using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SlikCache.Tests")]
[assembly: InternalsVisibleTo("SlikCache.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7")]

namespace Slik.Cache
{
    /// <summary>
    /// Distributed Cache Implementation 
    /// </summary>
    internal partial class SlikCache : PersistentState, IDistributedCache
    {
        private readonly MemoryDistributedCache _internalCache;
        private readonly HashSet<string> _slidingExpirations = new();
        private readonly ILogger<SlikCache> _logger;
        private readonly NamedLockFactory _lockFactory = new();
        private Guid _recordBeingAppendedLocally;
        private readonly ILoggerFactory _loggerFactory;
        
        public TimeSpan CommitTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public string LogLocation { get; }

        #region PersistentState implementation

        public SlikCache(IOptions<SlikOptions> options, ILoggerFactory loggerFactory) : base(
            path: options.Value.DataFolder,
            recordsPerPartition: options.Value.RecordsPerPartition,
            configuration: options.Value.PersistentStateOptions)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<SlikCache>();
            LogLocation = options.Value.DataFolder;
            _internalCache = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()), loggerFactory);            
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            _logger.LogDebug($"ApplyAsync started. Entry Length = {entry.Length}, timestamp = {entry.Timestamp}, snapshot = {entry.IsSnapshot}, term = {entry.Term}.");

            if (entry.Length > 0)
            {
                _logger.LogDebug($"Deserializing log entry into dictionary. Length = {entry.Length}, timestamp = {entry.Timestamp}.");

                var deserializedEntry = await entry.DeserializeFromJsonAsync().ConfigureAwait(false);

                if (deserializedEntry is CacheLogRecord record)
                {
                    bool isSameRecord = record.Id.Equals(_recordBeingAppendedLocally);

                    _logger.LogDebug(isSameRecord
                        ? "Same record, no lock" // it's the case when a leader updates itself
                        : "Not the same record, lock will be obtained");

                    using (isSameRecord
                        ? (AsyncLock.Holder?)null // no lock needed, it's the same record, we obtained a lock for it already and are inside of the code to apply it
                        : await _lockFactory.AcquireWriteLockAsync(record.Key).ConfigureAwait(false))
                    {
                        string action = string.Empty;
                        switch (record.Operation)
                        {
                            case CacheOperation.Update:
                                await _internalCache
                                    .SetAsync(record.Key, record.Value, record.Options ?? new())
                                    .ConfigureAwait(false);
                                action = "added to cache";
                                break;
                            case CacheOperation.Remove:
                                await _internalCache
                                    .RemoveAsync(record.Key)
                                    .ConfigureAwait(false);
                                action = "removed from cache";
                                break;
                            case CacheOperation.Refresh:
                                await _internalCache
                                    .RefreshAsync(record.Key)
                                    .ConfigureAwait(false);
                                action = "refreshed in cache";
                                break;
                        }
                        _logger.LogDebug($"Record with key '{record.Key}' was {action}");
                    }
                }
                else
                    _logger.LogWarning($"Unknown type encountered while deserializing: '{deserializedEntry?.GetType()}'");
            }
        }

        #endregion

        // delegate to handle leader redirection
        internal event Func<CacheLogRecord, CancellationToken, ValueTask<bool>>? RedirectHandler;
        internal event Func<TimeSpan, CancellationToken, Task<bool>>? ReplicateHandler;

        public class RemoteUpdateException : Exception
        {
            public RemoteUpdateException(string message) : base(message) { }
        }

        private async Task<long> AppendLocallyAsync(CacheLogRecord newRecord, CancellationToken token)
        {
            var newEntry = CreateJsonLogEntry(newRecord);
            long logIndex = await AppendAsync(newEntry, token).ConfigureAwait(false);
            _logger.LogDebug($"Log entry #{logIndex} has been added locally.");
            return logIndex;
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await _lockFactory.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }

        #region IDistributedCache implementation

        public byte[] Get(string key) => GetAsync(key).Result;

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            _logger.LogDebug($"Reading entry '{key}'");

            using (await _lockFactory.AcquireReadLockAsync(key, token).ConfigureAwait(false))
            {
                var result = await _internalCache.GetAsync(key, token).ConfigureAwait(false);

                // if there is a sliding expiration, refresh it
                if (_slidingExpirations.Contains(key))
                {
                    _ = BroadcastRefreshAsync(key, token);
                }

                return result;
            }
        }

        public void Refresh(string key) => RefreshAsync(key).Wait();

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            await _internalCache.RefreshAsync(key, token).ConfigureAwait(false);
            await BroadcastRefreshAsync(key, token).ConfigureAwait(false);
        }

        private async Task BroadcastRefreshAsync(string key, CancellationToken token = default)
        {
            var record = new CacheLogRecord(CacheOperation.Refresh, key, Array.Empty<byte>())
            {
                Id = Guid.NewGuid()
            }; 

            await RedirectApplyReplicateAsync(record, () => Task.FromResult(Array.Empty<byte>()), token).ConfigureAwait(false);
        }

        private async Task RedirectApplyReplicateAsync(CacheLogRecord record, Func<Task<byte[]>> localUpdateAction, CancellationToken token = default)
        {
            bool handled = false;

            do
            {
                handled = RedirectHandler != null && await RedirectHandler(record, token).ConfigureAwait(false);

                if (!handled)
                {
                    _logger.LogDebug("The change is not handled by the router, applying locally");

                    using (await _lockFactory.AcquireWriteLockAsync(record.Key, token).ConfigureAwait(false))
                    {
                        _recordBeingAppendedLocally = record.Id;
                        try
                        {
                            var fallbackValue = await localUpdateAction().ConfigureAwait(false);                            
                            
                            long logIndex = await AppendLocallyAsync(record, token).ConfigureAwait(false);

                            if (ReplicateHandler != null) // not in offline mode
                            {
                                try
                                {
                                    var currentTerm = Term;
                                    bool replicated = await ReplicateHandler(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
                                    bool sameTerm = currentTerm == Term;

                                    if (replicated && sameTerm)
                                    {
                                        _logger.LogDebug($"Log entry #{logIndex} has been replicated successfully.");

                                        if (await WaitForCommitAsync(logIndex, CommitTimeout, token).ConfigureAwait(false))
                                        {
                                            _logger.LogDebug($"Log entry #{logIndex} has been committed successfully.");
                                        }
                                        else
                                            throw new RemoteUpdateException($"Commit with index #{logIndex} unsuccessful.");
                                    }
                                    else
                                        throw new RemoteUpdateException(sameTerm
                                            ? $"Log entry #{logIndex} was not replicated successfully."
                                            : $"Term has changed from {currentTerm} to {Term} while replicating.");
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning(e,
                                        $"Error while updating remote storages. Rolling back changes and dropping uncommitted entry #{logIndex}");

                                    if (record.Operation != CacheOperation.Refresh)
                                    {
                                        if (fallbackValue != null && fallbackValue.Length > 0)
                                            await _internalCache.SetAsync(record.Key, fallbackValue, record.Options ?? new(), token);
                                        else
                                            await _internalCache.RemoveAsync(record.Key, token);
                                    }

                                    await DropAsync(logIndex, token).ConfigureAwait(false);

                                    handled = false;
                                }
                            }

                            handled = true;
                        }
                        finally
                        {
                            _recordBeingAppendedLocally = default;
                        }
                    }
                }

                if (!handled)
                    _logger.LogDebug("Retrying to redirect or apply locally after a failure");

            } while (!handled);
        }        

        public void Remove(string key) => RemoveAsync(key).Wait();

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            _logger.LogDebug($"Removing entry '{key}'");

            var record = new CacheLogRecord(CacheOperation.Remove, key, Array.Empty<byte>())
            {
                Id = Guid.NewGuid()
            };

            await RedirectApplyReplicateAsync(record, async () =>
            {
                var oldValue = await _internalCache.GetAsync(key, token).ConfigureAwait(false);
                if (oldValue != null)
                {
                    await _internalCache.RemoveAsync(key, token).ConfigureAwait(false);
                    _slidingExpirations.Remove(key);
                }
                return oldValue ?? Array.Empty<byte>();
            }, token).ConfigureAwait(false);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions? options) => SetAsync(key, value, options).Wait();

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions? options, CancellationToken token = default)
        {
            _logger.LogDebug($"Updating entry '{key}'");

            var record = new CacheLogRecord(CacheOperation.Update, key, value, options)
            {
                Id = Guid.NewGuid()
            };

            await RedirectApplyReplicateAsync(record, async () =>
            {
                var oldValue = await _internalCache.GetAsync(key, token).ConfigureAwait(false);                
                await _internalCache.SetAsync(key, value, options ?? new(), token);

                if (options?.SlidingExpiration != null)
                    _slidingExpirations.Add(key);
                else
                    _slidingExpirations.Remove(key);

                return oldValue ?? Array.Empty<byte>();
            }, token).ConfigureAwait(false);
        }
        #endregion
    }
}
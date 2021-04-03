using DotNext.IO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache
{
    internal partial class SlikCache
    {
        protected override SlikCacheSnapshotBuilder CreateSnapshotBuilder() => new(record => CreateJsonLogEntry(record), _loggerFactory);

        protected class SlikCacheSnapshotBuilder : SnapshotBuilder
        {
            private readonly MemoryDistributedCache _cache;
            private readonly HashSet<string> _keys = new();
            private readonly Func<CacheLogRecord, IDataTransferObject> _createEntryFunc;
            private readonly ILogger<SlikCacheSnapshotBuilder> _logger;

            public SlikCacheSnapshotBuilder(Func<CacheLogRecord, IDataTransferObject> createEntryFunc, ILoggerFactory loggerFactory) : base()
            {
                _createEntryFunc = createEntryFunc;
                _logger = loggerFactory.CreateLogger<SlikCacheSnapshotBuilder>();
                _cache = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()), loggerFactory);
            }

            protected override async ValueTask ApplyAsync(LogEntry entry)
            {
                var deserializedEntry = await entry.DeserializeFromJsonAsync().ConfigureAwait(false);

                if (deserializedEntry is CacheLogRecord record)
                {
                    string action;

                    switch (record.Operation)
                    {
                        case CacheOperation.Update:
                            _keys.Add(record.Key);
                            await _cache.SetAsync(record.Key, record.Value, record.Options ?? new()).ConfigureAwait(false);
                            action = "updated in snapshot";
                            break;
                        case CacheOperation.Remove:
                            await _cache.RemoveAsync(record.Key).ConfigureAwait(false);
                            _keys.Remove(record.Key);
                            action = "removed from snapshot";
                            break;
                        default:
                            action = "ignored";
                            break;
                    }

                    _logger.LogDebug($"Entry with key '{record.Key}' was {action}");
                }
                else
                    _logger.LogWarning($"Entry with timestamp '{entry.Timestamp}' is of unsupported type '{deserializedEntry?.GetType()}'");
            }

            public override async ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                _logger.LogDebug($"Writing {_keys.Count} entry(ies) from a snapshot...");

                foreach (string key in _keys)
                {
                    IDataTransferObject newEntry = _createEntryFunc(new CacheLogRecord(CacheOperation.Update, key, await _cache.GetAsync(key, token)));
                    await newEntry.WriteToAsync(writer, token).ConfigureAwait(false);
                    _logger.LogDebug($"Entry with a key '{key}' has been written from a snapshot.");
                }
            }
        }
    }
}

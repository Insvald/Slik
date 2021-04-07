using DotNext.Threading;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache
{
    public class NamedLockFactory : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, AsyncReaderWriterLock> _locks = new();
        
        public async Task<AsyncLock.Holder> AcquireWriteLockAsync(string name, CancellationToken token = default)
        {
            var namedLock = _locks.GetOrAdd(name, _ => new AsyncReaderWriterLock());
            return await namedLock.AcquireWriteLockAsync(token);            
        }

        public async Task<AsyncLock.Holder> AcquireReadLockAsync(string name, CancellationToken token = default)
        {
            var namedLock = _locks.GetOrAdd(name, _ => new AsyncReaderWriterLock());
            return await namedLock.AcquireReadLockAsync(token);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            foreach (var namedLock in _locks.Values)
                await namedLock.DisposeAsync();
        }
    }
}
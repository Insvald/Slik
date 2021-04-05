using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache
{
    public class CacheConsumer : IHostedService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<CacheConsumer> _logger;
        private readonly Task _workerTask;
        private readonly CancellationTokenSource _stoppingCts = new();
        private const string keyName = "key";
        private string? _value = null;
        private readonly string _historyFileName;

        public CacheConsumer(IDistributedCache cache, ILogger<CacheConsumer> logger, IConfiguration config)
        {
            _cache = cache;
            _logger = logger;
            _historyFileName = Path.Combine(config["folder"], "history.txt");
            File.Delete(_historyFileName);
            _workerTask = WorkerAsync(_stoppingCts.Token);
        }

        private async Task WorkerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string value = await _cache.GetStringAsync(keyName, token).ConfigureAwait(false);
                    if (value != _value)
                    {
                        _value = value;
                        File.AppendAllLines(_historyFileName, new[] { _value });
                    }
                    
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { return; }
                catch (Exception e)
                {
                    _logger.LogError($"Error occurred: {e}");
                }
            }
        }
        
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stoppingCts.Cancel();

            try
            {
                await _workerTask;
                _workerTask.Dispose();
            }
            finally
            {
                _stoppingCts.Dispose();
            }
        }
    }
}

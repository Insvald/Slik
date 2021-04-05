using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf.Grpc.Client;
using Slik.Cache.Grpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class SlikCacheIntegrationTests
    {
#if DEBUG
        private const string TestProjectPath = "..\\..\\..\\..\\..\\examples\\SlikNode\\bin\\Debug\\net6.0\\SlikNode.exe";
#else
        private const string TestProjectPath = "..\\..\\..\\..\\..\\examples\\SlikNode\\bin\\Release\\net6.0\\SlikNode.exe";
#endif

        private static Task RunInstances(int instanceCount, string executable, Func<int, string>? arguments = null, CancellationToken token = default)
        {
            List<Process> processList = new();

            for (int n = 0; n < instanceCount; n++)
            {
                var newProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments?.Invoke(n) ?? string.Empty,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty
                })
                    ?? throw new Exception($"Error creating {n}th process");

                processList.Add(newProcess);
            }

            return Task.WhenAll(processList.Select(async p =>
            {
                try
                {
                    await p.WaitForExitAsync(token);
                }
                catch (TaskCanceledException)
                {
                    p.Kill();
                }
            }));
        }

        [TestMethod]
        //[Ignore]
        public async Task Cluster_Consensus_HappyPath()
        {
            int instances = 3;
            int startPort = 3262;

            using var cts = new CancellationTokenSource();
            
            cts.CancelAfter(TimeSpan.FromSeconds(7));
                
            await RunInstances(instances, TestProjectPath, n =>
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"{startPort + n}");
                string memberList = $"{string.Join(",", Enumerable.Range(startPort, instances).Select(port => $"localhost:{port}")) }";
                return $"--port={startPort + n} --folder=\"{path}\" --members=\"{memberList}\" --testCache=true";
            }, cts.Token);

            // collect logs and compare history from each node
            List<string[]> history = new();
            for (int n = 0; n < instances; n++)
            {
                string historyFileName = Path.Combine($"{startPort + n}", "history.txt");
                var instanceHistory = await File.ReadAllLinesAsync(historyFileName);
                history.Add(instanceHistory);
            }

            // TODO align data in columns

            // output in columns
            string line = "";
            for (int n = 0; n < instances; n++)
                line += $"{startPort + n}\t";

            Console.WriteLine(line);
            Console.WriteLine("------------------------------------------------");

            // non-aligned output
            for (int i = 0; i < history.Max(h => h.Length); i++)
            {
                line = "";
                for (int n = 0; n < instances; n++)
                    line += $"{(history[n].Length > i ? history[n][i] : "")}\t";

                Console.WriteLine(line);
            }            
        }

        private static readonly HttpMessageHandler _httpHandler = new RaftClientHandlerFactory().CreateHandler("");

        private GrpcChannel GetChannel(string url) => 
            GrpcChannel.ForAddress(url, new GrpcChannelOptions
            {
                HttpHandler = _httpHandler
            });
        

        [TestMethod]
        public async Task Set_Value_GetsReplicated()
        {
            int instances = 3;
            int startPort = 3262;
            var expectedValue = new byte[] { 1, 2, 3 };

            using var cts = new CancellationTokenSource();

            var runTask = RunInstances(instances, TestProjectPath, n =>
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"{startPort + n}");
                string memberList = $"{string.Join(",", Enumerable.Range(startPort, instances).Select(port => $"localhost:{port}")) }";
                return $"--port={startPort + n} --folder=\"{path}\" --members=\"{memberList}\" --api=true";
            }, cts.Token);

            await Task.Delay(1000);

            try
            {
                {
                    using var channel = GetChannel($"https://localhost:{startPort}");
                    var service = channel.CreateGrpcService<ISlikCacheService>();
                    await service.Set(new SetRequest { Key = "key", Value = expectedValue });
                }

                {
                    using var channel = GetChannel($"https://localhost:{startPort + 1}");
                    var service = channel.CreateGrpcService<ISlikCacheService>();
                    var result = await service.Get(new KeyRequest { Key = "key" });
                    Assert.IsTrue(expectedValue.SequenceEqual(result.Value));
                }

                {
                    using var channel = GetChannel($"https://localhost:{startPort + 2}");
                    var service = channel.CreateGrpcService<ISlikCacheService>();
                    var result = await service.Get(new KeyRequest { Key = "key" });
                    Assert.IsTrue(expectedValue.SequenceEqual(result.Value));
                }
            }
            finally
            {
                cts.Cancel();
                await runTask;
            }
        }

        //[TestMethod]
        //public async Task Cluster_NewNode_GetsValues()
        //{
        //    throw new NotImplementedException();
        //}

        //[TestMethod]
        //public async Task Cluster_ChaosOfUpdates_GetsTheLastValue()
        //{
        //    throw new NotImplementedException();
        //}
    }
}

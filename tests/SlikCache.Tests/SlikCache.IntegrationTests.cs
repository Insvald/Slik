using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ProtoBuf.Grpc.Client;
using Slik.Cache.Grpc.V1;
using Slik.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cache.Tests
{
    [TestClass]
    public class SilkCacheIntegrationTests
    {
#if DEBUG
        private const string TestProjectPath = "..\\..\\..\\..\\..\\examples\\SlikNode\\bin\\Debug\\net6.0\\SlikNode.exe";
#else
        private const string TestProjectPath = "..\\..\\..\\..\\..\\examples\\SlikNode\\bin\\Release\\net6.0\\SlikNode.exe";
#endif

        private static readonly HttpMessageHandler _httpHandler;
        private static readonly X509Certificate2 _certificate;

        [ClassCleanup]
        public static void Cleanup()
        {
            _httpHandler.Dispose();
            _certificate.Dispose();
        }

        static SilkCacheIntegrationTests()
        {
            _certificate = Node.Startup.LoadCertificate("node.pfx");

            var certifierMock = new Mock<ICommunicationCertifier>();
            certifierMock.Setup(c => c.SetupClient(It.IsAny<SslClientAuthenticationOptions>())).Callback<SslClientAuthenticationOptions>(opt =>
            {
                opt.ClientCertificates = new(new[] { _certificate });
                opt.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            });

            _httpHandler = new RaftClientHandlerFactory(certifierMock.Object).CreateHandler("");
        }

        private Task RunInstances(int instanceCount, string executable, int startPort, string? arguments = null, CancellationToken token = default)
        {
            List<Process> processList = new();

            for (int n = 0; n < instanceCount; n++)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"{startPort + n}");
                string memberList = $"{string.Join(",", Enumerable.Range(startPort, instanceCount).Select(port => $"localhost:{port}")) }";

                var newProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"--port={startPort + n} --folder=\"{path}\" --members=\"{memberList}\" {arguments}",
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
        public async Task Cluster_Consensus_HappyPath()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;

            using var cts = new CancellationTokenSource();

            cts.CancelAfter(TimeSpan.FromSeconds(7));

            await RunInstances(instances, TestProjectPath, startPort, "--testCache=true", cts.Token);

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

        private async ValueTask<T> UseGrpcService<T>(int port, Func<ISlikCacheService, ValueTask<T>> useFunction)
        {
            using var channel = GrpcChannel.ForAddress($"https://localhost:{port}", new GrpcChannelOptions
            {
                HttpHandler = _httpHandler
            });

            var service = channel.CreateGrpcService<ISlikCacheService>();

            return await useFunction(service);
        }

        private async Task UseGrpcService(int port, Func<ISlikCacheService, Task> useAction) =>
            await UseGrpcService(port, async service => { await useAction(service); return true; });

        [TestMethod]
        public async Task SetAndRemove_GetReplicated()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;
            var expectedValue = new byte[] { 1, 2, 3 };

            using var cts = new CancellationTokenSource();

            var runTask = RunInstances(instances, TestProjectPath, startPort, "--api=true", cts.Token);

            await Task.Delay(500);

            try
            {
                await UseGrpcService(startPort, service => service.Set(new SetRequest { Key = "key", Value = expectedValue }));

                await Task.Delay(500);

                // checking set
                for (int port = startPort; port < startPort + instances; port++)
                {
                    var result = await UseGrpcService(port, service => service.Get(new KeyRequest { Key = "key" }));
                    Assert.IsTrue(expectedValue.SequenceEqual(result.Value));
                }

                await UseGrpcService(startPort, service => service.Remove(new KeyRequest { Key = "key" }));

                await Task.Delay(500);

                // checking remove
                for (int port = startPort; port < startPort + instances; port++)
                {
                    var result = await UseGrpcService(port, service => service.Get(new KeyRequest { Key = "key" }));
                    Assert.IsTrue(result.Value.Length == 0);
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

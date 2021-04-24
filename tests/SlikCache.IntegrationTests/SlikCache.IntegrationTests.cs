using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

namespace Slik.Cache.IntegrationTests
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
            var generator = new CertificateGenerator(Mock.Of<ILogger<CertificateGenerator>>());
            var certifier = new SelfSignedCertifier(Options.Create(new CertificateOptions { UseSelfSigned = true }), generator, Mock.Of<ILogger<SelfSignedCertifier>>());
            var rootCertificate = certifier.RootCertificate;                                        

            //_certificate = Node.Startup.LoadCertificate("node.pfx");
            _certificate = generator.Generate("test client cert", rootCertificate, CertificateAuthentication.Client);

            var certifierMock = new Mock<ICommunicationCertifier>();
            certifierMock.Setup(c => c.SetupClient(It.IsAny<SslClientAuthenticationOptions>())).Callback<SslClientAuthenticationOptions>(opt =>
            {
                opt.ClientCertificates = new(new[] { _certificate });
                opt.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            });

            _httpHandler = new RaftClientHandlerFactory(certifierMock.Object).CreateHandler("");
        }

        private static Task RunInstances(int instanceCount, string executable, int startPort, Func<int, string> memberListFunc, string? arguments = null, CancellationToken token = default)
        {
            List<Process> processList = new();

            for (int n = 0; n < instanceCount; n++)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), $"{startPort + n}");

                var newProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"--port={startPort + n} --folder=\"{path}\" --members=\"{memberListFunc(n)}\" --use-self-signed {arguments}",
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

        private static Task RunInstances(int instanceCount, string executable, int startPort, string? arguments = null, CancellationToken token = default)
        {
            string memberList = $"{string.Join(",", Enumerable.Range(startPort, instanceCount).Select(port => $"localhost:{port}")) }";
            return RunInstances(instanceCount, executable, startPort, _ => memberList, arguments, token);
        }

        [TestMethod]
        public async Task Cluster_Consensus_HappyPath()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;

            using var cts = new CancellationTokenSource();

            cts.CancelAfter(TimeSpan.FromSeconds(7));

            await RunInstances(instances, TestProjectPath, startPort, "--testCache", cts.Token);

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

        private static async ValueTask<T> UseGrpcService<I, T>(int port, Func<I, ValueTask<T>> useFunction) where I : class
        {
            using var channel = GrpcChannel.ForAddress($"https://localhost:{port}", new GrpcChannelOptions
            {
                HttpHandler = _httpHandler
            });

            var service = channel.CreateGrpcService<I>();

            return await useFunction(service);
        }

        private static async Task UseGrpcService<I>(int port, Func<I, Task> useAction) where I : class =>
            await UseGrpcService<I, bool>(port, async service => { await useAction(service); return true; });

        [TestMethod]
        public async Task SetAndRemove_GetReplicated()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;

            using var cts = new CancellationTokenSource();

            var runTask = RunInstances(instances, TestProjectPath, startPort, "--api", cts.Token);

            await Task.Delay(3000);

            try
            {
                await SetGetRemoveAssertAsync(startPort, instances);
            }
            finally
            {
                cts.Cancel();
                await runTask;
            }
        }

        private static async Task SetGetRemoveAssertAsync(int startPort, int instances)
        {
            var expectedValue = new byte[] { 1, 2, 3 };

            await UseGrpcService<ISlikCacheService>(startPort, service => service.Set(new SetRequest { Key = "key", Value = expectedValue }));

            await Task.Delay(1000);

            // checking set
            for (int port = startPort; port < startPort + instances; port++)
            {
                var result = await UseGrpcService<ISlikCacheService, ValueResponse>(port, service => service.Get(new KeyRequest { Key = "key" }));
                Assert.IsTrue(expectedValue.SequenceEqual(result.Value));
            }

            await UseGrpcService<ISlikCacheService>(startPort, service => service.Remove(new KeyRequest { Key = "key" }));

            await Task.Delay(1000);

            // checking remove
            for (int port = startPort; port < startPort + instances; port++)
            {
                var result = await UseGrpcService<ISlikCacheService, ValueResponse>(port, service => service.Get(new KeyRequest { Key = "key" }));
                Assert.IsTrue(result.Value.Length == 0);
            }
        }

        [TestMethod]
        public async Task AddMemberTest()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;

            using var cts = new CancellationTokenSource();

            var runTask = RunInstances(instances, TestProjectPath, startPort, n => n switch
            {
                0 => $"https://localhost:{startPort}",
                1 => $"https://localhost:{startPort},https://localhost:{startPort + 1}",
                2 => $"https://localhost:{startPort},https://localhost:{startPort + 2}",
                _ => throw new ArgumentOutOfRangeException(),
            },
            "--api", cts.Token);

            try
            {
                await Task.Delay(4000);
                await SetGetRemoveAssertAsync(startPort, instances);
            }
            finally
            {
                cts.Cancel();
                await runTask;
            }
        }

        [TestMethod]
        public async Task RemoveMemberTest()
        {
            int instances = 3;
            int startPort = SlikOptions.DefaultPort;

            using var cts = new CancellationTokenSource();

            var runTask = RunInstances(instances, TestProjectPath, startPort, n => n switch
            {
                0 => $"https://localhost:{startPort}",
                1 => $"https://localhost:{startPort},https://localhost:{startPort + 1}",
                2 => $"https://localhost:{startPort},https://localhost:{startPort + 2}",
                //3 => $"https://localhost:{startPort},https://localhost:{startPort + 3}",
                _ => throw new ArgumentOutOfRangeException(),
            },
            "--api", cts.Token);

            try
            {
                await Task.Delay(4000);
                await SetGetRemoveAssertAsync(startPort, instances);
                await UseGrpcService<ISlikMembershipService>(startPort, m => m.Remove(new MemberRequest { Member = $"https://localhost:{startPort + 2}" }));
                await Task.Delay(1000);

                var expectedValue = new byte[] { 3, 2, 1 };

                await UseGrpcService<ISlikCacheService>(startPort, service => service.Set(new SetRequest { Key = "key", Value = expectedValue }));

                await Task.Delay(1000);

                // checking that value is not replicated to the removed node
                var result = await UseGrpcService<ISlikCacheService, ValueResponse>(startPort + 2, service => service.Get(new KeyRequest { Key = "key" }));
                Assert.IsFalse(expectedValue.SequenceEqual(result.Value));

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

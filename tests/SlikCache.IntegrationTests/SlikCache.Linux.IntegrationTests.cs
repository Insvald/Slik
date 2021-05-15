using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ProtoBuf.Grpc.Client;
using Slik.Cache.Grpc.V1;
using Slik.Security;
using System.IO;
using System.Net.Security;
using System.Threading.Tasks;
using TestEnvironment.Docker;

namespace Slik.Cache.IntegrationTests
{
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class SlikCacheLinuxIntegrationTests
    {
        private static DockerEnvironment? _environment;
        private static Container? _testContainer;

        private const string TestContainerName = "slik-node";
        private const string TestContainerName2 = "slik-node-2";
        private const string TestContainerName3 = "slik-node-3";
        private const string DockerFileName = "..\\..\\..\\..\\..\\examples\\SlikNode\\Dockerfile";
        private const string DockerFileName2 = "..\\..\\..\\..\\..\\examples\\SlikNode\\Dockerfile2";
        private const string DockerFileName3 = "..\\..\\..\\..\\..\\examples\\SlikNode\\Dockerfile3";

        [ClassInitialize]
        public static async Task Init(TestContext _)
        {
            //string folder = Directory.GetCurrentDirectory();

            // Create the environment using builder pattern.
            _environment = new DockerEnvironmentBuilder()
                .AddFromDockerfile(TestContainerName, Path.GetFileName(DockerFileName), context: Path.GetDirectoryName(DockerFileName), containerWaiter: new HttpContainerWaiter("/", httpPort: 3092))
                //.AddFromDockerfile(TestContainerName2, Path.GetFileName(DockerFileName2), context: Path.GetDirectoryName(DockerFileName2), containerWaiter: new HttpContainerWaiter("/", httpPort: 3093))
                //.AddFromDockerfile(TestContainerName3, Path.GetFileName(DockerFileName3), context: Path.GetDirectoryName(DockerFileName3), containerWaiter: new HttpContainerWaiter("/", httpPort: 3094))
                .Build();

            // Up it.
            await _environment.Up();

            // Play with containers.
            _testContainer = _environment.GetContainer(TestContainerName);
        }

        [ClassCleanup]
        public static async Task Cleanup()
        {
            if (_environment != null && _testContainer != null)
            {
                await _environment.Down();
                await _testContainer.DisposeAsync();
                await _environment.DisposeAsync();
            }
        }

        [TestMethod]
        [Ignore]
        public void Test()
        {
            using var _certificate = Node.Startup.LoadCertificate("node.pfx");

            var certifierMock = new Mock<ICommunicationCertifier>();
            certifierMock.Setup(c => c.SetupClient(It.IsAny<SslClientAuthenticationOptions>())).Callback<SslClientAuthenticationOptions>(opt =>
            {
                opt.ClientCertificates = new(new[] { _certificate });
                opt.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            });

            using var _httpHandler = new RaftClientHandlerFactory(certifierMock.Object).CreateHandler("");

            using var channel = GrpcChannel.ForAddress($"https://localhost:3092", new GrpcChannelOptions
            {
                HttpHandler = _httpHandler
            });

            var service = channel.CreateGrpcService<ISlikCacheService>();
            var value = service.Get(new KeyRequest { Key = "key" });
        }
    }
}

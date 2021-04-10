using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Slik.Security.Tests
{
    [TestClass]
    public class CaSignedCertifierTests : CertifierTestsBase
    {
        public CaSignedCertifierTests()
        {
            using var rootCert = Generator.Generate("test CA root");
            
            var options = new CertificateOptions 
            { 
                SelfSignedUsage = SelfSignedUsage.None,
                ServerCertificate = Generator.Generate("test service", rootCert, CertificateAuthentication.Server),
                ClientCertificate = Generator.Generate("test client", rootCert, CertificateAuthentication.Client)
            };

            Certifier = new CaSignedCertifier(Options.Create(options), Mock.Of<ILogger<CaSignedCertifier>>());

            TestServerCertificate = options.ServerCertificate;
            TestClientCertificate = options.ClientCertificate;
        }

        [TestCleanup]
        public override void Cleanup()
        {
            TestServerCertificate?.Dispose();
            TestClientCertificate?.Dispose();           
            
            base.Cleanup();
        }        
    }
}

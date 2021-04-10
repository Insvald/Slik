using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security.Tests
{
    public abstract class CertifierTestsBase
    {        
        public CertifierTestsBase()
        {
            Generator = new CertificateGenerator(Mock.Of<ILogger<CertificateGenerator>>());
            TestWrongCertificate = Generator.Generate("fake cert");
        }

        protected ICommunicationCertifier? Certifier { get; set; }
        protected ICertificateGenerator Generator { get; }
        protected X509Certificate2? TestClientCertificate { get; set; }
        protected X509Certificate2? TestServerCertificate { get; set; }
        protected X509Certificate2 TestWrongCertificate { get; set; }


        [TestCleanup]
        public virtual void Cleanup()
        {
            TestWrongCertificate.Dispose();
        }

        [TestMethod]
        public void SetupClient_CorrectServerCerificate_ValidatesSuccessfully()
        {
            SslClientAuthenticationOptions options = new();
            Certifier!.SetupClient(options);
            bool? result = options.RemoteCertificateValidationCallback?.Invoke(this, TestServerCertificate, null, SslPolicyErrors.None);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SetupClient_WrongServerCerificate_DoesNotValidate()
        {
            SslClientAuthenticationOptions options = new();
            Certifier!.SetupClient(options);
            bool? result = options.RemoteCertificateValidationCallback?.Invoke(this, TestWrongCertificate, null, SslPolicyErrors.None);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SetupServer_CorrectClientCertificate_ValidatesSuccessfully()
        {
            HttpsConnectionAdapterOptions options = new();
            Certifier!.SetupServer(options);
            bool? result = options.ClientCertificateValidation?.Invoke(TestClientCertificate, null, SslPolicyErrors.None);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SetupServer_WrongClientCertificate_DoesNotValidate()
        {
            HttpsConnectionAdapterOptions options = new();
            Certifier!.SetupServer(options);
            bool? result = options.ClientCertificateValidation?.Invoke(TestWrongCertificate, null, SslPolicyErrors.None);
            Assert.IsFalse(result);
        }
    }
}

using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security.Tests
{
    public record CertifierFixture
    {
        private readonly Func<X509Certificate2?> _serverFactory;
        private readonly Func<X509Certificate2?> _clientFactory;

        public ICommunicationCertifier Certifier { get; }
        public X509Certificate2? TestServerCertificate => _serverFactory();
        public X509Certificate2? TestClientCertificate => _clientFactory();

        public CertifierFixture(ICommunicationCertifier certifier, X509Certificate2? serverCertificate, X509Certificate2? clientCertificate) =>
            (Certifier, _serverFactory, _clientFactory) = (certifier, () => serverCertificate, () => clientCertificate);

        public CertifierFixture(ICommunicationCertifier certifier, Lazy<X509Certificate2?> serverCertificateFactory, Lazy<X509Certificate2?> clientCertificateFactory) =>
            (Certifier, _serverFactory, _clientFactory) = (certifier, () => serverCertificateFactory.Value, () => clientCertificateFactory.Value);
    }

    public abstract class CertifierTestsBase
    {
        protected readonly CertifierFixture Fixture;
        private readonly X509Certificate2 _testWrongCertificate;

        public CertifierTestsBase()
        {
            var generator = new CertificateGenerator(Mock.Of<ILogger<CertificateGenerator>>());

            Fixture = GetFixture(generator);
            _testWrongCertificate = generator.Generate("test fake");
        }

        [TestCleanup]
        public virtual void Cleanup()
        {
            Fixture.TestServerCertificate?.Dispose();
            Fixture.TestClientCertificate?.Dispose();
        }

        protected abstract CertifierFixture GetFixture(CertificateGenerator generator);

        [TestMethod]       
        public void SetupClient_CorrectServerCerificate_ValidatesSuccessfully()
        {
            SslClientAuthenticationOptions options = new();
            Fixture.Certifier.SetupClient(options);                       
            bool? result = options.RemoteCertificateValidationCallback?.Invoke(this, Fixture.TestServerCertificate, null, SslPolicyErrors.None);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SetupClient_WrongServerCerificate_DoesNotValidate()
        {
            SslClientAuthenticationOptions options = new();
            Fixture.Certifier.SetupClient(options);
            bool? result = options.RemoteCertificateValidationCallback?.Invoke(this, _testWrongCertificate, null, SslPolicyErrors.None);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SetupServer_CorrectClientCertificate_ValidatesSuccessfully()
        {
            HttpsConnectionAdapterOptions options = new();
            Fixture.Certifier.SetupServer(options);
            bool result = options.ClientCertificateValidation?.Invoke(Fixture.TestClientCertificate, null, SslPolicyErrors.None) ?? true;
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SetupServer_WrongClientCertificate_DoesNotValidate()
        {
            HttpsConnectionAdapterOptions options = new();
            Fixture.Certifier.SetupServer(options);
            bool result = options.ClientCertificateValidation?.Invoke(_testWrongCertificate, null, SslPolicyErrors.None) ?? false;
            Assert.IsFalse(result);
        }
    }    
}

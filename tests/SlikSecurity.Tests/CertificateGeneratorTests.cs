using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Slik.Security.Tests
{
    [TestClass]
    public class CertificateGeneratorTests
    {
        private readonly X509Certificate2 _rootCertificate;
        private const string RootCertificateName = "test root";
        private const string ServiceCertificateName = "test service";
        private const string ClientCertificateName = "test client";
        private readonly ICertificateGenerator _generator = new CertificateGenerator(Mock.Of<ILogger<CertificateGenerator>>());

        public CertificateGeneratorTests()
        {
            _rootCertificate = _generator.Generate(RootCertificateName);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _rootCertificate.Dispose();
        }

        private static void VerifySelfSigned(X509Certificate2 certificate, string certificateName)
        {
            Assert.IsNotNull(certificate);

            var chain = new X509Chain();
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            bool isBuildSuccessFull = chain.Build(certificate);

            Assert.IsTrue(isBuildSuccessFull, string.Join("\n", chain.ChainStatus));
            Assert.AreEqual($"CN={certificateName}", certificate.Subject);
            Assert.IsTrue(certificate.HasPrivateKey);
        }

        private static bool DoesSupportOid(X509Certificate2 certificate, string oidValue) => certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(c => c.EnhancedKeyUsages.Cast<Oid>())
            .Any(oid => oid.Value == oidValue);

        [TestMethod]
        public void Generate_RootCertificate_ProvidesValidCertificate()
        {
            VerifySelfSigned(_rootCertificate, RootCertificateName);

            // check basic constraints extension
            bool basicConstraintsExist = _rootCertificate.Extensions.OfType<X509BasicConstraintsExtension>().Any();
            Assert.IsTrue(basicConstraintsExist);
        }

        [TestMethod]
        public void Generate_ServiceCertificate_ProvidesCertificateSuitableForService()
        {
            using var cert = _generator.Generate(ServiceCertificateName, _rootCertificate, CertificateAuthentication.Server);

            VerifySelfSigned(cert, ServiceCertificateName);
            bool oidSupported = DoesSupportOid(cert, CertificateGenerator.ServerAuthenticationOid);
            Assert.IsTrue(oidSupported, "No service authentication oid in the certificate.");
        }

        [TestMethod]
        public void Generate_ClientCertificate_ProvidesCertificateSuitableForClient()
        {
            using var cert = _generator.Generate(ClientCertificateName, _rootCertificate, CertificateAuthentication.Client);

            VerifySelfSigned(cert, ClientCertificateName);
            bool oidSupported = DoesSupportOid(cert, CertificateGenerator.ClientAuthenticationOid);
            Assert.IsTrue(oidSupported, "No client authentication oid in the certificate.");
        }

        [TestMethod]
        public void Generate_DefaultAuth_DoesNotSupportClientAndServiceUsage()
        {
            using var cert = _generator.Generate(ClientCertificateName, _rootCertificate);

            VerifySelfSigned(cert, ClientCertificateName);
            
            bool oidSupported = DoesSupportOid(cert, CertificateGenerator.ClientAuthenticationOid);
            Assert.IsFalse(oidSupported, "Client authentication oid found in the certificate.");

            oidSupported = DoesSupportOid(cert, CertificateGenerator.ServerAuthenticationOid);
            Assert.IsFalse(oidSupported, "Service authentication oid found in the certificate.");
        }

        [TestMethod]
        public void Generate_BothAuth_SupportsClientAndServiceUsage()
        {
            using X509Certificate2 cert = _generator.Generate(ClientCertificateName, _rootCertificate, CertificateAuthentication.Both);

            VerifySelfSigned(cert, ClientCertificateName);

            bool oidSupported = DoesSupportOid(cert, CertificateGenerator.ClientAuthenticationOid);
            Assert.IsTrue(oidSupported, "No client authentication oid in the certificate.");

            oidSupported = DoesSupportOid(cert, CertificateGenerator.ServerAuthenticationOid);
            Assert.IsTrue(oidSupported, "No service authentication oid in the certificate.");
        }

        [TestMethod]
        public void ValidateSelfSigned_DerivedCertificates_Confirms()
        {
            var serviceCert = _generator.Generate(ServiceCertificateName, _rootCertificate, CertificateAuthentication.Server);
            bool isValidated = _generator.ValidateSelfSigned(serviceCert, _rootCertificate);
            Assert.IsTrue(isValidated);

            var clientCert = _generator.Generate(ClientCertificateName, _rootCertificate, CertificateAuthentication.Client);
            isValidated = _generator.ValidateSelfSigned(clientCert, _rootCertificate);
            Assert.IsTrue(isValidated);
        }

        [TestMethod]
        public void ValidateSelfSigned_WrongCertificate_DoesNotConfirm()
        {
            var serviceCert = _generator.Generate("service", _rootCertificate, CertificateAuthentication.Server);
            bool isValidated = _generator.ValidateSelfSigned(serviceCert, _generator.Generate("wrong root"));
            Assert.IsFalse(isValidated);
        }
    }
}

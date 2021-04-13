using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Slik.Security.Tests
{
    public abstract class CaSignedTestsBase : CertifierTestsBase
    {
        protected abstract bool UseClientCertificates { get; }

        protected override CertifierFixture GetFixture(CertificateGenerator generator)
        {
            using var rootCert = generator.Generate("test CA root");

            var options = new CertificateOptions
            {
                UseSelfSigned = false,
                ServerCertificate = generator.Generate("test service", rootCert, CertificateAuthentication.Server),
                ClientCertificate = UseClientCertificates ? generator.Generate("test client", rootCert, CertificateAuthentication.Client) : null
            };

            return new CertifierFixture(
                new CaSignedCertifier(Options.Create(options), Mock.Of<ILogger<CaSignedCertifier>>()),
                options.ServerCertificate,
                options.ClientCertificate);
        }
    }

    [TestClass]
    public class CaSignedWithoutClientCertificatesTests : CaSignedTestsBase
    {
        protected override bool UseClientCertificates { get; } = false;
    }

    [TestClass]
    public class CaSignedWithClientCertificatesTests : CaSignedTestsBase
    {
        protected override bool UseClientCertificates { get; } = true;
    }

}

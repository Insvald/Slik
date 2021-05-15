using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security.Tests
{
    public abstract class SelfSignedTests : CertifierTestsBase
    {
        protected readonly X509Store Store = new(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);        

        protected X509Certificate2? GetRootCertificateFromStore()
        {
            var foundCertificates = Store.Certificates.Find(X509FindType.FindBySubjectName, SelfSignedCertifier.SelfSignedSubject, false);
            return foundCertificates.Count > 0 ? foundCertificates[0] : null;
        }

        protected void DeleteRootFromStore()
        {
            using var rootCertificate = GetRootCertificateFromStore();

            if (rootCertificate != null)
                Store.Remove(rootCertificate);
        }

        public override void Cleanup()
        {
            DeleteRootFromStore();

            Store.Close();
            Store.Dispose();

            base.Cleanup();
        }
    }

    // Self-signed certificates when the root exists in the store
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class SelfSignedWithExisting : SelfSignedTests
    {
        protected override CertifierFixture GetFixture(CertificateGenerator generator)
        {
            DeleteRootFromStore(); // clean up previous

            using var rootCertificate = generator.Generate(SelfSignedCertifier.SelfSignedSubject);

            Store.Add(rootCertificate);

            return new CertifierFixture(
                new SelfSignedCertifier(Options.Create(new CertificateOptions { UseSelfSigned = true }), generator, Mock.Of<ILogger<SelfSignedCertifier>>()),
                generator.Generate("test service", rootCertificate, CertificateAuthentication.Server),
                generator.Generate("test client", rootCertificate, CertificateAuthentication.Client));
        }
    }

    // Self-signed certificates when the root doesn't exist in the store
    [TestClass]
#if NET5_0
    [TestCategory(".Net 5")]
#else
    [TestCategory(".Net 6")]
#endif
    public class SelfSignedWithoutExisting : SelfSignedTests
    {
        private X509Certificate2 CreateCertificate(CertificateGenerator generator, string name, CertificateAuthentication auth)
        {
            using var rootCertificate = GetRootCertificateFromStore();

            return rootCertificate != null
                ? generator.Generate(name, rootCertificate, auth)
                : throw new Exception("No root certificate in the store");
        }

        protected override CertifierFixture GetFixture(CertificateGenerator generator)
        {
            DeleteRootFromStore();

            return new CertifierFixture(
                new SelfSignedCertifier(Options.Create(new CertificateOptions { UseSelfSigned = true }), generator, Mock.Of<ILogger<SelfSignedCertifier>>()),
                new Lazy<X509Certificate2?>(() => CreateCertificate(generator, "test service", CertificateAuthentication.Server)),
                new Lazy<X509Certificate2?>(() => CreateCertificate(generator, "test client", CertificateAuthentication.Client)));
        }
    }    
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security.Tests
{
    public abstract class SelfSignedCertifierTestsBase : CertifierTestsBase
    {
        private readonly X509Store _store;
        private readonly X509Certificate2 _rootCertificate;

        public SelfSignedCertifierTestsBase(CertificateOptions options, bool getRootBeforeCertifier)
        {
            _store = new X509Store(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);

            if (getRootBeforeCertifier)
                _rootCertificate = GetRootCertificateForTests(_store);

            Certifier = new SelfSignedCertifier(Options.Create(options), Generator, Mock.Of<ILogger<SelfSignedCertifier>>());

            _rootCertificate ??= GetRootCertificateForTests(_store);

            TestServerCertificate = Generator.Generate("test service", _rootCertificate, CertificateAuthentication.Server);
            TestClientCertificate = Generator.Generate("test client", _rootCertificate, CertificateAuthentication.Client);
        }

        protected abstract X509Certificate2 GetRootCertificateForTests(X509Store store);

        [TestCleanup]
        public override void Cleanup()
        {
            TestServerCertificate?.Dispose();
            TestClientCertificate?.Dispose();

            _store.Remove(_rootCertificate);
            _store.Close();
            _store.Dispose();

            _rootCertificate.Dispose();

            (Certifier as IDisposable)?.Dispose();

            base.Cleanup();
        }
    }

    [TestClass]
    public class UseSelfSignedCertifierTests : SelfSignedCertifierTestsBase
    {
        public UseSelfSignedCertifierTests() : base(new CertificateOptions { SelfSignedUsage = SelfSignedUsage.Use }, true) { }

        protected override X509Certificate2 GetRootCertificateForTests(X509Store store)
        {
            var result = Generator.Generate(SelfSignedCertifier.SelfSignedSubject);
            store.Add(result);
            return result;
        }
    }

    [TestClass]
    public class CreateSelfSignedCertifierTests : SelfSignedCertifierTestsBase
    {
        public CreateSelfSignedCertifierTests() : base(new CertificateOptions { SelfSignedUsage = SelfSignedUsage.Create }, false) { }

        protected override X509Certificate2 GetRootCertificateForTests(X509Store store)
        {
            var results = store.Certificates.Find(X509FindType.FindBySubjectName, SelfSignedCertifier.SelfSignedSubject, false);

            Assert.IsTrue(results.Count > 0, "Root self-signed certificate has not been created");

            return results[0];
        }
    }

}

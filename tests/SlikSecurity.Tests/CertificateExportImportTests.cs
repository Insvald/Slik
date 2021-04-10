using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security.Tests
{
    [TestClass]
    public class CertificateExportImportTests
    {
        private readonly X509Certificate2 _rootCertificate;
        private readonly ICertificateGenerator _generator = new CertificateGenerator(Mock.Of<ILogger<CertificateGenerator>>());
        private readonly ICertificateExportImport _exportImport = new CertificateExportImport(Mock.Of<ILogger<CertificateExportImport>>());

        public CertificateExportImportTests()
        {
            _rootCertificate = _generator.Generate("test root");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _rootCertificate.Dispose();
        }

        private void ExportToFileTest(bool withKey, Action<string, string> asserts)
        {
            string fileName = "cert";
            _exportImport.ExportToFile(_rootCertificate, fileName, withKey);

            string crtFileName = Path.ChangeExtension(fileName, CertificateExportImport.CrtFileExtension);
            string keyFileName = Path.ChangeExtension(fileName, CertificateExportImport.KeyFileExtension);

            try
            {
                asserts(crtFileName, keyFileName);
            }
            finally
            {
                foreach (string fileToDelete in new[] { crtFileName, keyFileName })
                    File.Delete(fileToDelete);
            }
        }

        [TestMethod]
        public void Import_ExportedSecret_ProvidesIdenticalCertificate()
        {
            string secret = _exportImport.ExportToSecret(_rootCertificate);
            X509Certificate2 imported = _exportImport.ImportFromSecret(secret);

            Assert.AreEqual(_rootCertificate.Thumbprint, imported.Thumbprint);
        }

        [TestMethod]
        public void Import_ExportedSecret_ProvidesCertificateWithPrivateKey()
        {
            string secret = _exportImport.ExportToSecret(_rootCertificate);
            X509Certificate2 imported = _exportImport.ImportFromSecret(secret);

            Assert.IsTrue(imported.HasPrivateKey);
        }               

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ExportToFile_CreatesCorrectFiles(bool saveKey)
        {
            ExportToFileTest(saveKey, (crtFileName, keyFileName) =>
            {
                bool crtExists = File.Exists(crtFileName);
                bool keyExists = File.Exists(keyFileName);

                Assert.IsTrue(crtExists);
                Assert.AreEqual(saveKey, keyExists);
            });
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ImportFromFile_ImportsValidCertificate(bool saveKey)
        {
            ExportToFileTest(saveKey, (crtFileName, _) =>
            {
                var cert = _exportImport.ImportFromFile(crtFileName, saveKey);

                if (saveKey)
                    Assert.IsTrue(cert.HasPrivateKey);

                bool isValidated = _generator.ValidateSelfSigned(cert, _rootCertificate);
                Assert.IsTrue(isValidated);
            });
        }
    }

}

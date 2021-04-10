using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Slik.Security
{
    public class CertificateExportImport : ICertificateExportImport
    {
        private const string RsaPrivateKeyFileHeader = "-----BEGIN RSA PRIVATE KEY-----";
        private const string RsaPrivateKeyFileFooter = "-----END RSA PRIVATE KEY-----";
        private const string CrtFileHeader = "-----BEGIN CERTIFICATE-----";
        private const string CrtFileFooter = "-----END CERTIFICATE-----";
        
        private readonly ILogger<CertificateExportImport> _logger;
        private readonly string ExportPassword = typeof(CertificateExportImport).Name;

        public const string CrtFileExtension = "crt";
        public const string KeyFileExtension = "key";

        public CertificateExportImport(ILogger<CertificateExportImport> logger)
        {
            _logger = logger;
        }

        public string ExportToSecret(X509Certificate2 certificate)
        {
            _logger.LogDebug($"Exporting certificate '{certificate.Subject}' to secret");
            return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, ExportPassword));
        }

        public X509Certificate2 ImportFromSecret(string secret)
        {
            var result = new X509Certificate2(Convert.FromBase64String(secret), ExportPassword, X509KeyStorageFlags.Exportable);
            _logger.LogDebug($"Certificate '{result.Subject}' has been imported from secret");
            return result;
        }
        
        /// <summary>
        /// Exports a certificate to a crt file
        /// </summary>
        /// <param name="certificate">A certificate to export</param>
        /// <param name="fileName">A file name for crt file</param>
        /// <param name="saveRsaKey">If true, exports a private key to a key file</param>
        public void ExportToFile(X509Certificate2 certificate, string fileName, bool saveRsaKey = false)
        {
            _logger.LogDebug($"Exporting certificate '{certificate.Subject}' to '{fileName}'");

            var sb = new StringBuilder();
            sb.AppendLine(CrtFileHeader);
            sb.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert)));
            sb.Append(CrtFileFooter);
            File.WriteAllText(Path.ChangeExtension(fileName, CertificateExportImport.CrtFileExtension), sb.ToString());

            if (saveRsaKey)
            {
                string keyFileName = Path.ChangeExtension(fileName, CertificateExportImport.KeyFileExtension);
                _logger.LogDebug($"Saving RSA key for certificate '{certificate.Subject}' to '{keyFileName}'");

                using RSA? key = certificate.GetRSAPrivateKey();

                if (key != null)
                {
                    using RSA intermediate = RSA.Create();

                    // import-export, workaround for not working export
                    intermediate.ImportEncryptedPkcs8PrivateKey(ExportPassword,
                        key.ExportEncryptedPkcs8PrivateKey(ExportPassword,
                            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1)), out int bytesRead);

                    if (bytesRead > 0)
                    {
                        sb.Clear();
                        sb.AppendLine(RsaPrivateKeyFileHeader);
                        sb.AppendLine(Convert.ToBase64String(intermediate.ExportRSAPrivateKey()));
                        sb.Append(RsaPrivateKeyFileFooter);
                        File.WriteAllText(keyFileName, sb.ToString());
                    }
                }
            }
        }        

        /// <summary>
        /// Imports previously exported certificate from a file
        /// </summary>
        /// <param name="fileName">crt or cer file name</param>
        /// <param name="loadRsaKey">if true, fileName.key should exist</param>
        /// <returns></returns>
        public X509Certificate2 ImportFromFile(string fileName, bool loadRsaKey = false)
        {
            _logger.LogDebug($"Importing certificate from '{fileName}'");

            using var pubOnly = new X509Certificate2(fileName);

            if (loadRsaKey)
            {
                string keyFileName = Path.ChangeExtension(fileName, CertificateExportImport.KeyFileExtension);
                _logger.LogDebug($"Loading RSA key from '{keyFileName}'");

                using RSA rsa = RSA.Create();
                string keyFileContent = File.ReadAllText(keyFileName).Trim();
                if (keyFileContent.StartsWith(RsaPrivateKeyFileHeader) &&
                    keyFileContent.EndsWith(RsaPrivateKeyFileFooter))
                {
                    rsa.ImportRSAPrivateKey(
                        Convert.FromBase64String(
                            keyFileContent.Substring(
                                RsaPrivateKeyFileHeader.Length,
                                keyFileContent.Length - RsaPrivateKeyFileHeader.Length - RsaPrivateKeyFileFooter.Length)),
                        out int bytesRead);

                    if (bytesRead > 0)
                    {
                        using X509Certificate2 pubPrivEphemeral = pubOnly.CopyWithPrivateKey(rsa);
                        return new X509Certificate2(pubPrivEphemeral.Export(X509ContentType.Pfx));
                    }
                }
            }

            return new X509Certificate2(pubOnly.Export(X509ContentType.Pfx));
        }
    }
}

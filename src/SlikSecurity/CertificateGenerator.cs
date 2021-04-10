using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    public class CertificateGenerator : ICertificateGenerator
    {
        private const int RsaKeySize = 2048;
        public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
        public const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

        private readonly ILogger<CertificateGenerator> _logger;

        public CertificateGenerator(ILogger<CertificateGenerator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generates new service and client certificates suitable for TLS communications
        /// </summary>
        /// <param name="subject">A subject for the certificate (e.g. "CN=something, OU=something")</param>
        /// <param name="issuerCertificate">The CA root certificate. If null, a new self signed root certificate is issued</param>
        /// <param name="serverAuthentication">True, if suitable for service authentication, false otherwise. If null (default), supports both service and client usage</param>
        /// <returns>The newly generated certificate</returns>
        public X509Certificate2 Generate(string certificateName, X509Certificate2? issuerCertificate = null, CertificateAuthentication authentication = CertificateAuthentication.None)
        {
            // the implementation idea is taken from https://stackoverflow.com/questions/48196350/generate-and-sign-certificate-request-using-pure-net-framework            
            _logger.LogDebug($"Attempting to create a certificate '{certificateName}'.");

            bool isRoot = issuerCertificate == null;

            using RSA key = RSA.Create(RsaKeySize);

            var request = new CertificateRequest($"CN={certificateName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isRoot, false, 0, isRoot));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(isRoot
                ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature
                : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                false));

            if (!isRoot)
            {
                _logger.LogDebug($"Adding extensions for non-root certificate '{certificateName}'");

                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    authentication switch
                    {
                        CertificateAuthentication.None => new OidCollection { },
                        CertificateAuthentication.Client => new OidCollection { new Oid(ClientAuthenticationOid) },
                        CertificateAuthentication.Server => new OidCollection { new Oid(ServerAuthenticationOid) },
                        CertificateAuthentication.Both => new OidCollection
                        {
                            new Oid(ServerAuthenticationOid),
                            new Oid(ClientAuthenticationOid)
                        },
                        _ => throw new NotSupportedException()
                    },
                    true));

                var sanBuilder = new SubjectAlternativeNameBuilder();

                foreach (string dns in new[] { NetworkUtils.GetLocalMachineName(), "localhost" })
                    sanBuilder.AddDnsName(dns);

                foreach (IPAddress ip in NetworkUtils.GetLocalIPAddresses().Union(new[] { IPAddress.Loopback }))
                    sanBuilder.AddIpAddress(ip);

                request.CertificateExtensions.Add(sanBuilder.Build());
            }

            X509Certificate2 certificateWithKey;

            if (issuerCertificate == null) // if (isRoot) generates nullable warnings
            {
                certificateWithKey = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + TimeSpan.FromDays(365*10) /*10 years*/);
            }
            else
            {
                _logger.LogDebug($"Creating exportable non-root certificate '{certificateName}'");

                using (X509Certificate2
                    intermediate = request.Create(issuerCertificate, DateTimeOffset.UtcNow, issuerCertificate.NotAfter, BitConverter.GetBytes(DateTime.UtcNow.ToBinary())))
                {
                    // attaching the private key
                    certificateWithKey = intermediate.CopyWithPrivateKey(key);
                }
            }

            try
            {
                // temporary password for import/export workaround
                string ExportPassword = typeof(CertificateGenerator).Name;

                // we need to export/import a certificate before use: https://github.com/dotnet/runtime/issues/23749
                var result = new X509Certificate2(certificateWithKey.Export(X509ContentType.Pfx, ExportPassword), ExportPassword, X509KeyStorageFlags.Exportable);
                
                _logger.LogDebug($"The certificate '{certificateName}' has been created successfully.");
                return result;
            }
            finally
            {
                certificateWithKey.Dispose();
            }
        }

        /// <summary>
        /// Validate that the self-signed certificate is signed by the supplied root
        /// </summary>
        /// <param name="cert">A certificate to validate</param>
        /// <param name="chain">A chain to use for validation</param>
        /// <param name="rootCertificate">A root certificate - supposed issuer</param>
        /// <returns></returns>
        public bool ValidateSelfSigned(X509Certificate2 cert, X509Certificate2 rootCertificate)
        {
            _logger.LogDebug($"Starting validation for a certificate with subject '{cert.Subject}'");

            using var chain = new X509Chain();
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            // for child certificates we need to build a chain with custom root trust
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.ExtraStore.Add(rootCertificate);

            bool isValid =
                chain.Build(cert) &&
                // check if the chain root is our root
                // we can check the tumbnails, it will be quicker
                chain.ChainElements[^1].Certificate.RawData.SequenceEqual(rootCertificate.RawData);

            if (!isValid)
                _logger.LogWarning($"Validation has failed for a certificate with subject '{cert.Subject}'. Chain status: {string.Join(",", chain.ChainStatus.Select(c => c.Status))}");
            else
                _logger.LogDebug($"Validation has succeeded for a certificate with subject '{cert.Subject}'.");

            return isValid;
        }
    }
}

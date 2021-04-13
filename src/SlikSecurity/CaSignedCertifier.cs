using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    /// <summary>
    /// Setups client and server certificates for CA signed mode, setups client and server validation to check for them
    /// Does not perform chain or revocation checks
    /// </summary>
    public class CaSignedCertifier : CertifierBase
    {
        public CaSignedCertifier(IOptions<CertificateOptions> options, ILogger<CaSignedCertifier> logger)
            : base(options, logger)
        {
            if (options.Value.UseSelfSigned)
                throw new Exception("Wrong certifier");

            if (options.Value.ServerCertificate == null)
                throw new ArgumentNullException(nameof(options.Value.ServerCertificate));
        }

        protected override bool ValidateClientCertificate(X509Certificate2 certificate) =>
            certificate.Thumbprint.Equals(Options.ClientCertificate?.Thumbprint);

        protected override bool ValidateServerCertificate(X509Certificate2 certificate) =>
            certificate.Thumbprint.Equals(Options.ServerCertificate?.Thumbprint);
    }
}

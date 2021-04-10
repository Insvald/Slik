using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    /// <summary>
    /// Setups client and server certificates for self-signed mode, setups client and server validation to check for them
    /// Does not perform chain or revocation checks
    /// </summary>
    public class SelfSignedCertifier : CertifierBase, IDisposable
    {
        internal const string SelfSignedSubject = "Slik Root CA";
        private readonly X509Certificate2 _rootCertificate;
        private readonly ICertificateGenerator _generator;

        public SelfSignedCertifier(IOptions<CertificateOptions> options, ICertificateGenerator generator, ILogger<SelfSignedCertifier> logger)
            : base(options, logger)
        { 
            if (options.Value.SelfSignedUsage == SelfSignedUsage.None)
                throw new Exception("Wrong certifier");

            _generator = generator;

            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);

            var results = store.Certificates.Find(X509FindType.FindBySubjectName, SelfSignedSubject, false);
            var existingCertificate = results.Count > 0 ? results[0] : null;

            if (Options.SelfSignedUsage == SelfSignedUsage.Create)
            {
                // recreate
                if (existingCertificate != null)
                {
                    store.Remove(existingCertificate);
                    existingCertificate.Dispose();
                }   
                
                _rootCertificate = generator.Generate(SelfSignedSubject);
                store.Add(_rootCertificate);
            }
            else
            {
                _rootCertificate = existingCertificate ?? throw new Exception($"Slik certificates with '{SelfSignedSubject}' are not found in the certificate store");
            }

            store.Close();

            Options.ServerCertificate =
                generator.Generate($"Service based on '{SelfSignedSubject}'", _rootCertificate, CertificateAuthentication.Server);
            Options.ClientCertificate =
                generator.Generate($"Client based on '{SelfSignedSubject}'", _rootCertificate, CertificateAuthentication.Client);
        }

        public void Dispose()
        {
            _rootCertificate?.Dispose();
            Options.ClientCertificate?.Dispose();
            Options.ServerCertificate?.Dispose();
        }

        protected override bool ValidateClientCertificate(X509Certificate2 certificate) =>
            _generator.ValidateSelfSigned(certificate, _rootCertificate);

        protected override bool ValidateServerCertificate(X509Certificate2 certificate) =>
            _generator.ValidateSelfSigned(certificate, _rootCertificate);
    }
}

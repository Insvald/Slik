using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    public abstract class CertifierBase : ICommunicationCertifier
    {
        private readonly ILogger<CertifierBase> _logger;
        
        protected CertificateOptions Options { get; }

        public CertifierBase(IOptions<CertificateOptions> options, ILogger<CertifierBase> logger)
        {
            _logger = logger;
            Options = options.Value;
        }

        protected abstract bool ValidateServerCertificate(X509Certificate2 certificate);
        protected abstract bool ValidateClientCertificate(X509Certificate2 certificate);

        public void SetupClient(SslClientAuthenticationOptions clientOptions)
        {
            if (Options.ClientCertificate != null)
                clientOptions.ClientCertificates = new X509CertificateCollection(new[] { Options.ClientCertificate });

            clientOptions.RemoteCertificateValidationCallback = (_, certificate, __, errors) =>
            {
                if (certificate != null)
                {
                    using var certificateWrapper = new X509Certificate2(certificate);
                    var result = (errors == SslPolicyErrors.None || errors == SslPolicyErrors.RemoteCertificateChainErrors) &&
                        ValidateServerCertificate(certificateWrapper);

                    if (!result)
                        _logger.LogWarning($"Server certificate '{certificate.Subject}' hasn't passed validation. Policy errors: {errors}");

                    return result;
                }
                else
                {
                    _logger.LogWarning("Certificate is empty, no validation");
                    return false;
                }
            };
        }

        public void SetupServer(HttpsConnectionAdapterOptions serverOptions)
        {
            serverOptions.ServerCertificate = Options.ServerCertificate;

            serverOptions.ClientCertificateMode = Options.ClientCertificate != null
                ? ClientCertificateMode.RequireCertificate
                : ClientCertificateMode.NoCertificate;

            serverOptions.CheckCertificateRevocation = !Options.UseSelfSigned;

            if (Options.ClientCertificate != null)
            {
                serverOptions.ClientCertificateValidation = (certificate, _, errors) =>
                {
                    var result = (errors == SslPolicyErrors.None || errors == SslPolicyErrors.RemoteCertificateChainErrors) &&
                        ValidateClientCertificate(certificate);

                    if (!result)
                        _logger.LogWarning($"Client certificate '{certificate.Subject}' hasn't passed validation. Policy errors: {errors}");

                    return result;
                };
            }
        }
    }    
}

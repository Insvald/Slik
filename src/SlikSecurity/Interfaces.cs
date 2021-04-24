using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

[assembly: InternalsVisibleTo("SlikCache.Tests")]
[assembly: InternalsVisibleTo("SlikCache.IntegrationTests")]
[assembly: InternalsVisibleTo("SlikSecurity.Tests")]
namespace Slik.Security
{
    public enum CertificateAuthentication { None, Client, Server, Both };

    public interface ICertificateGenerator
    {
        X509Certificate2 Generate(string certificateName, X509Certificate2? issuerCertificate = null, 
            CertificateAuthentication authentication = CertificateAuthentication.None);

        bool ValidateSelfSigned(X509Certificate2 cert, X509Certificate2 rootCertificate);        
    }

    public interface ICertificateExportImport
    {
        string ExportToSecret(X509Certificate2 certificate);
        X509Certificate2 ImportFromSecret(string secret);

        void ExportToFile(X509Certificate2 cert, string fileName, bool saveRsaKey = false);
        X509Certificate2 ImportFromFile(string fileName, bool loadRsaKey = false);        
    }

    public interface ICommunicationCertifier
    {
        void SetupServer(HttpsConnectionAdapterOptions serverOptions);
        void SetupClient(SslClientAuthenticationOptions clientOptions);
    }
}

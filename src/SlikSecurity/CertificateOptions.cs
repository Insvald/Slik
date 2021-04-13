using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    public class CertificateOptions
    {
        public bool UseSelfSigned { get; set; }
        public X509Certificate2? ServerCertificate { get; set; }
        public X509Certificate2? ClientCertificate { get; set; }

        public void CopyTo(CertificateOptions options)
        {
            options.UseSelfSigned = UseSelfSigned;
            options.ServerCertificate = ServerCertificate;
            options.ClientCertificate = ClientCertificate;
        }
    }
}

using System.Security.Cryptography.X509Certificates;

namespace Slik.Security
{
    public enum SelfSignedUsage { None, Create, Use };

    public class CertificateOptions
    {
        public SelfSignedUsage SelfSignedUsage { get; set; }        
        public X509Certificate2? ServerCertificate { get; set; }
        public X509Certificate2? ClientCertificate { get; set; }

        public void CopyTo(CertificateOptions options)
        {
            options.SelfSignedUsage = SelfSignedUsage;
            options.ServerCertificate = ServerCertificate;
            options.ClientCertificate = ClientCertificate;
        }
    }
}

using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator
{
    public static class X509Certificate2Extensions
    {
        public static string CERTIFICATE_HEADER_PEM => "-----BEGIN CERTIFICATE-----\n";
        public static string CERTIFICATE_FOOTER_PEM => "\n-----END CERTIFICATE-----";

        public static string PRIVATE_KEY_HEADER_PEM => "-----BEGIN PRIVATE KEY-----\n";
        public static string PRIVATE_KEY_FOOTER_PEM => "\n-----END PRIVATE KEY-----";

        public static X509Certificate2? RootCACert(this X509Certificate2 cert)
        {
            var chain = new X509Chain();
            chain.Build(cert);

            // CA is always last element of a full CA chain.

            var chainElement = chain.ChainElements[chain.ChainElements.Count - 1];

            foreach (var status in chainElement.ChainElementStatus)
            {
                if (status.Status == X509ChainStatusFlags.PartialChain)
                {
                    return null;
                }
            }
            return chainElement.Certificate;
        }

        public static (string CertificatePem, string PrivateKeyPem) ExportCertAndPrivateKeyPem(this X509Certificate2 cert)
        {
            //cert.pem
            // Convert the certificate to PEM format
            string certificatePem = $"{CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(cert.Export(X509ContentType.Cert))}{CERTIFICATE_FOOTER_PEM}";

            // Convert the private key to PEM format
            string privateKeyPem = cert.ExportPrivateKeyToPem();

            return (certificatePem, privateKeyPem);
        }

        public static string ExportCARootPem(this X509Certificate2 cert, ILogger _logger)
        {
            // Get the issuer's distinguished name (DN)
            var issuerDn = cert.Issuer;

            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            // Find the issuer certificate by DN
            var issuerCertificates = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, issuerDn, false);

            string caCertificatePem;

            if (issuerCertificates.Count > 0)
            {
                var issuerCertificate = issuerCertificates[0];
                caCertificatePem = $"{CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(issuerCertificate.Export(X509ContentType.Cert))}{CERTIFICATE_FOOTER_PEM}";
            }
            else
            {
                _logger.LogDebug($"The root CA information for {cert.FriendlyName} cannot be found.");
                return string.Empty;
            }

            store.Close();

            return caCertificatePem;
        }

        private static string ExportPrivateKeyToPem(this X509Certificate2 certificate)
        {
            var privateKey = certificate.GetRSAPrivateKey();
            if (privateKey == null) return string.Empty;

            var pkcs8privatekey = privateKey.ExportPkcs8PrivateKey();// certificate.PrivateKey.ExportPkcs8PrivateKey();
            var pem = Convert.ToBase64String(pkcs8privatekey);
            return $"{PRIVATE_KEY_HEADER_PEM}{pem}{PRIVATE_KEY_FOOTER_PEM}";
        }
    }
}

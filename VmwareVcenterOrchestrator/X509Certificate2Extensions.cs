using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

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
            _logger.MethodEntry();
            _logger.LogDebug($"----- EXTRACTING ROOT CA from {cert.FriendlyName} -----", new { cert });

            // Get the issuer's distinguished name (DN)
            var issuerDn = cert.Issuer;

            _logger.LogDebug($"cert.Issuer = {cert.Issuer}");

            _logger.LogTrace("building cert chain from provided cert");
            X509Chain certChain = new X509Chain();
            if (!certChain.Build(cert)) 
            {
                _logger.LogDebug($"The chain cannot be extracted from the certificate data.");
                return string.Empty;
            };

            var elementCount = certChain.ChainElements.Count;
            _logger.LogTrace($"success: cert chain has {elementCount} entries.");

            var rootChainPem = string.Empty;

            for (var i = 1; i < elementCount; i++) {
                rootChainPem += $"{CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(certChain.ChainElements[i].Certificate.Export(X509ContentType.Cert))}{CERTIFICATE_FOOTER_PEM}";
            }            
            _logger.LogTrace($"root chain = {rootChainPem}");
                                    
            //var issuerCert = certChain.ChainElements.First(ce => ce.Certificate.SubjectName.Name.ToLower() == issuerDn.ToLower())?.Certificate;
            
            //if (issuerCert == null)
            //{
            //    _logger.LogDebug($"Unable to find trusted root with subject distinguished name of {issuerDn} in chain.");
            //    return string.Empty;
            //}
            //_logger.LogTrace($"Found issuer cert named {issuerCert.FriendlyName}");

            //var caCertificatePem = $"{CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(issuerCert.Export(X509ContentType.Cert))}{CERTIFICATE_FOOTER_PEM}";

            return rootChainPem;
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

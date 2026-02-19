
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
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
                                    
            return rootChainPem;
        }

        private static string ExportPrivateKeyToPem(this X509Certificate2 certificate)
        {
            var privateKey = certificate.GetRSAPrivateKey();
            if (privateKey == null) return string.Empty;

            var pkcs8privatekey = privateKey.ExportPkcs8PrivateKey();
            var pem = Convert.ToBase64String(pkcs8privatekey);
            return $"{PRIVATE_KEY_HEADER_PEM}{pem}{PRIVATE_KEY_FOOTER_PEM}";
        }
    }
}

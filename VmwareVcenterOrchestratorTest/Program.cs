// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// See https://aka.ms/new-console-template for more information
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using Keyfactor.Orchestrators.Common.Enums;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestratorTest.Program
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Program p = new Program();

            await p.TestGetSslCertificate();

            var caSubjectName = Environment.GetEnvironmentVariable("VCENTER_CA_SUBJECT_NAME") ?? string.Empty;
            var caCertificate = CreateCACertificate(caSubjectName);
            var caCertificatePem = $"{X509Certificate2Extensions.CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(caCertificate.Export(X509ContentType.Cert))}{X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM}";

            var signedCertSubjectName = Environment.GetEnvironmentVariable("VCENTER_SIGNED_SUBJECT_NAME") ?? string.Empty;
            var signedCertificate = CreateSignedCertificate(caCertificate, signedCertSubjectName);

            (string CertificatePem, string PemKey) = ConvertCertificateToPemStrings(signedCertificate);

            var certReq = new VCenterTlsCertSet
            {
                cert = CertificatePem,
                key = PemKey,
                root_cert = caCertificatePem
            };

            await p.TestAddCertificate(certReq);
            p.TestRemoveCertificate(signedCertificate);
        }

        public Program()
        {
            string ClientMachine = Environment.GetEnvironmentVariable("VCENTER_HOSTNAME") ?? string.Empty;

            var properties = new VcenterProperties()
            {
                ServerUsername = Environment.GetEnvironmentVariable("VCENTER_USERNAME") ?? string.Empty,
                ServerPassword = Environment.GetEnvironmentVariable("VCENTER_PASSWORD") ?? string.Empty,
                ServerUseSsl = "true"
            };

            Client = new VmwareVcenterClient(ClientMachine, properties.ServerUsername, properties.ServerPassword);
        }

        private VmwareVcenterClient Client { get; }

        public async Task TestGetSslCertificate()
        {
            Console.Write("Getting Vcenter Certificates...\n");
            foreach (CurrentInventoryItem inventoryItem in FormatSslCert(await Client.GetVcenterSslCertificate()).ToList())
            {
                Console.Write($"Found certificate called {inventoryItem.Alias}\n");
            }
        }

        public async Task TestAddCertificate(VCenterTlsCertSet newCert)
        {
            Console.Write("Adding Vcenter Certificate...\n");
            await Client.ReplaceVcenterSslCertificate(newCert);
            Console.Write($"Added certificate\n");
        }

        public async Task TestRemoveCertificate(X509Certificate2 cert)
        {
            //Console.Write("Removing Vcenter Certificate...\n");
            //mock the vCenter 
            
        }

        public static (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(X509Certificate2 certificate)
        {
            // Convert the certificate to PEM format
            string certificatePem = $"{X509Certificate2Extensions.CERTIFICATE_HEADER_PEM}{Convert.ToBase64String(certificate.Export(X509ContentType.Cert))}{X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM}";

            // Convert the private key to PEM format
            string privateKeyPem = ExportPrivateKeyToPem(certificate);

            return (certificatePem, privateKeyPem);
        }

        private static string ExportPrivateKeyToPem(X509Certificate2 certificate)
        {
            var privateKey = certificate.PrivateKey;

            if (privateKey is RSA or ECDsa)
            {
                byte[] pkcs8PrivateKey = certificate.PrivateKey.ExportPkcs8PrivateKey();
                string pem = Convert.ToBase64String(pkcs8PrivateKey);
                return $"{X509Certificate2Extensions.PRIVATE_KEY_HEADER_PEM}{pem}{X509Certificate2Extensions.PRIVATE_KEY_FOOTER_PEM}";
            }

            // Add support for other key types if needed

            throw new NotSupportedException("Unsupported private key algorithm");
        }

        public static X509Certificate2 CreateCACertificate(string subjectName)
        {
            var rsa = RSA.Create(4096);
            var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));

            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.NonRepudiation |
                    X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DataEncipherment |
                    X509KeyUsageFlags.KeyAgreement |
                    X509KeyUsageFlags.KeyCertSign |
                    X509KeyUsageFlags.CrlSign, false));

            var caCertificate = req.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddYears(10)));

            return new X509Certificate2(caCertificate.Export(X509ContentType.Pfx, "password"), "password",
                X509KeyStorageFlags.Exportable);
        }

        public static X509Certificate2 CreateSignedCertificate(X509Certificate2 caCertificate, string subjectName)
        {
            // Create a new RSA key pair for the new certificate
            RSA rsa = RSA.Create(2048);

            // Create a certificate request for the new certificate
            var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add basic extensions to the certificate request
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(subjectName);
            req.CertificateExtensions.Add(sanBuilder.Build());

            // Sign the certificate request with the root CA's private key
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddYears(1);

            var signedCert =
                req.Create(caCertificate, notBefore, notAfter, req.PublicKey.EncodedKeyValue.RawData);

            return new X509Certificate2(signedCert.CopyWithPrivateKey(rsa));
        }

        public string GetTrustedRootChainIdentifier(X509Certificate2 certificate)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(certificate);

            var rootCertificate = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;

            return rootCertificate.Thumbprint;
        }

        public IEnumerable<CurrentInventoryItem> FormatSslCert(VCenterTlsCertInfo SslCert)
        {
            var inventoryItems = new List<CurrentInventoryItem>();

            // Vcenter certs are in PEM format
            //Remove the BEGIN/END
            SslCert.cert = SslCert.cert.Replace(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM, string.Empty).Replace(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM, string.Empty);

            // Create new inventory item for the certificate
            List<string> certList = new List<string> { SslCert.cert };

            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = SslCert.subject_alternative_name[0], //.subject_dn, 
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = certList
            };
            inventoryItems.Add(inventoryItem);
            return inventoryItems;
        }
    }
}
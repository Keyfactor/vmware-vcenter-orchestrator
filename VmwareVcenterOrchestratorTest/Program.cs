// See https://aka.ms/new-console-template for more information
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using System.Text;
using Newtonsoft.Json;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestratorTest.Program
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Program p = new Program();

            p.TestGetSslCertificate();

            X509Certificate2 caCertificate = CreateCACertificate("caserver.example.com");
            string caCertificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(caCertificate.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";

            X509Certificate2 signedCertificate = CreateSignedCertificate(caCertificate, "server.example.com");
            
            (string CertificatePem, string PemKey) = ConvertCertificateToPemStrings(signedCertificate);

            VcenterCertificateManagementVcenterTlsSet certReq = new VcenterCertificateManagementVcenterTlsSet
            {
                cert = CertificatePem,
                key = PemKey,
                root_cert = caCertificatePem
            };
            
            p.TestAddCertificate(certReq);
        }

        public Program()
        {
            string ClientMachine = Environment.GetEnvironmentVariable("VCENTER_HOSTNAME") ?? string.Empty;
            
            VcenterProperties properties = new VcenterProperties()
            {
                ServerUsername = Environment.GetEnvironmentVariable("VCENTER_USERNAME") ?? string.Empty,
                ServerPassword = Environment.GetEnvironmentVariable("VCENTER_PASSWORD") ?? string.Empty,
                ServerUseSsl = "true"
            };

            Client = new VmwareVcenterClient(ClientMachine, properties.ServerUsername, properties.ServerPassword);
        }

        private VmwareVcenterClient Client { get; }

        public void TestGetSslCertificate()
        {
            Console.Write("Getting Vcenter Certificates...\n");
            foreach (CurrentInventoryItem inventoryItem in Client.GetVcenterSslCertificate())
            {
                Console.Write($"Found certificate called {inventoryItem.Alias}\n");
            }
        }

        public void TestAddCertificate(VcenterCertificateManagementVcenterTlsSet newCert)
        {
            Console.Write("Adding Vcenter Certificate...\n");
            Client.AddVcenterSslCertificate(newCert);
        }
        
        private static X509Certificate2 GetSelfSignedCert(string hostname)
        {
            RSA rsa = RSA.Create(2048);
            CertificateRequest req = new CertificateRequest($"CN={hostname}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            
            SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
            subjectAlternativeNameBuilder.AddDnsName(hostname);
            req.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));        
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("2.5.29.32.0"), new Oid("1.3.6.1.5.5.7.3.1") }, false));
            
            X509Certificate2 selfSignedCert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
            Console.Write($"Created self-signed certificate for \"{hostname}\" with thumbprint {selfSignedCert.Thumbprint}\n");
            return selfSignedCert;
        }
        
        public static (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(X509Certificate2 certificate)
        {
            // Convert the certificate to PEM format
            string certificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";

            // Convert the private key to PEM format
            string privateKeyPem = ExportPrivateKeyToPem(certificate);

            return (certificatePem, privateKeyPem);
        }

        private static string ExportPrivateKeyToPem(X509Certificate2 certificate)
        {
            AsymmetricAlgorithm privateKey = certificate.PrivateKey;

            if (privateKey is RSA rsa)
            {
                return ExportRSAPrivateKeyToPem(rsa);
            }
            else if (privateKey is ECDsa ecdsa)
            {
                return ExportECDsaPrivateKeyToPem(ecdsa);
            }
            // Add support for other key types if needed

            throw new NotSupportedException("Unsupported private key algorithm");
        }

        private static string ExportRSAPrivateKeyToPem(RSA rsa)
        {
            byte[] pkcs8PrivateKey = rsa.ExportPkcs8PrivateKey();
            string pem = Convert.ToBase64String(pkcs8PrivateKey, Base64FormattingOptions.InsertLineBreaks);
            return $"-----BEGIN PRIVATE KEY-----\n{pem}\n-----END PRIVATE KEY-----";
        }

        private static string ExportECDsaPrivateKeyToPem(ECDsa ecdsa)
        {
            byte[] pkcs8PrivateKey = ecdsa.ExportPkcs8PrivateKey();
            string pem = Convert.ToBase64String(pkcs8PrivateKey, Base64FormattingOptions.InsertLineBreaks);
            return $"-----BEGIN PRIVATE KEY-----\n{pem}\n-----END PRIVATE KEY-----";
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
            CertificateRequest req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add basic extensions to the certificate request
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("devvcsa.epicpki.local");
            req.CertificateExtensions.Add(sanBuilder.Build());
            
            // Sign the certificate request with the root CA's private key
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddYears(1);

            X509Certificate2 signedCert = req.Create(caCertificate, notBefore, notAfter, req.PublicKey.EncodedKeyValue.RawData);

            return new X509Certificate2(signedCert.CopyWithPrivateKey(rsa));
        }
    }
}
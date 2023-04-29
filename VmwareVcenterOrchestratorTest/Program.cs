// See https://aka.ms/new-console-template for more information
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestratorTest.Program
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Program p = new Program();

            p.TestGetSslCertificate();
            
            string caSubjectName = Environment.GetEnvironmentVariable("VCENTER_CA_SUBJECT_NAME") ?? string.Empty;
            X509Certificate2 caCertificate = CreateCACertificate(caSubjectName);
            string caCertificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(caCertificate.Export(X509ContentType.Cert))}\n-----END CERTIFICATE-----";

            string signedCertSubjectName = Environment.GetEnvironmentVariable("VCENTER_SIGNED_SUBJECT_NAME") ?? string.Empty;
            X509Certificate2 signedCertificate = CreateSignedCertificate(caCertificate, signedCertSubjectName);
            
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
            Client.ReplaceVcenterSslCertificate(newCert);
        }

        public static (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(X509Certificate2 certificate)
        {
            // Convert the certificate to PEM format
            string certificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(certificate.Export(X509ContentType.Cert))}\n-----END CERTIFICATE-----";

            // Convert the private key to PEM format
            string privateKeyPem = ExportPrivateKeyToPem(certificate);

            return (certificatePem, privateKeyPem);
        }

        private static string ExportPrivateKeyToPem(X509Certificate2 certificate)
        {
            AsymmetricAlgorithm privateKey = certificate.PrivateKey;

            if (privateKey is RSA or ECDsa)
            {
                byte[] pkcs8PrivateKey = certificate.PrivateKey.ExportPkcs8PrivateKey();
                string pem = Convert.ToBase64String(pkcs8PrivateKey);
                return $"-----BEGIN PRIVATE KEY-----\n{pem}\n-----END PRIVATE KEY-----";
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
            CertificateRequest req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add basic extensions to the certificate request
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            SubjectAlternativeNameBuilder sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(subjectName);
            req.CertificateExtensions.Add(sanBuilder.Build());

            // Sign the certificate request with the root CA's private key
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = DateTimeOffset.UtcNow.AddYears(1);

            X509Certificate2 signedCert =
                req.Create(caCertificate, notBefore, notAfter, req.PublicKey.EncodedKeyValue.RawData);

            return new X509Certificate2(signedCert.CopyWithPrivateKey(rsa));
        }
    }
}
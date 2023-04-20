using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    public class Management : VmwareVcenterJob<Management>, IManagementJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Management>();

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            _logger.LogDebug("Beginning Vmware Vcenter Management Job");

            Initialize(config.CertificateStoreDetails);

            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            try
            {
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        _logger.LogDebug("Adding certificate to Vcenter");

                        PerformAddition(config);

                        _logger.LogDebug("Add operation complete.");

                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    //case CertStoreOperationType.Remove:
                    //_logger.LogDebug("Removing certificate from App Gateway");

                    //GatewayClient.RemoveAppGatewaySslCertificate(config.JobCertificate.Alias);

                    //_logger.LogDebug("Remove operation complete.");
                    //result.Result = OrchestratorJobStatusJobResult.Success;
                    //break;
                    default:
                        _logger.LogDebug("Invalid management operation type: {0}", config.OperationType);
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }

        private void PerformAddition(ManagementJobConfiguration config)
        {
            byte[] pkcs12CertBytes = Convert.FromBase64String(config.JobCertificate.Contents);

            // Create a new MemoryStream from the byte array
            MemoryStream stream = new MemoryStream(pkcs12CertBytes);

            // Create a new Pkcs12Store from the stream and password
            Pkcs12Store pkcs12Store = new Pkcs12Store(stream, config.JobCertificate.PrivateKeyPassword.ToCharArray());

            // Extract the certificate, private key, and root CA
            string certificatePem = null;
            string pemPrivateKey = null;
            string rootCertificatePem = null;

            foreach (string alias in pkcs12Store.Aliases)
            {
                if (pkcs12Store.IsKeyEntry(alias))
                {
                    X509CertificateEntry certEntry = pkcs12Store.GetCertificate(alias);
                    var cert = certEntry.Certificate;
                    X509Certificate2 certificate = new X509Certificate2(cert.GetEncoded());
                    certificatePem =
                        $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";


                    AsymmetricKeyEntry keyEntry = pkcs12Store.GetKey(alias);
                    var key = keyEntry.Key;
                    RsaPrivateCrtKeyParameters rsaPrivateKey = (RsaPrivateCrtKeyParameters)key;

                    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();

                    RSAParameters rsaParameters = new RSAParameters
                    {
                        Modulus = rsaPrivateKey.Modulus.ToByteArrayUnsigned(),
                        Exponent = rsaPrivateKey.PublicExponent.ToByteArrayUnsigned(),
                        P = rsaPrivateKey.P.ToByteArrayUnsigned(),
                        Q = rsaPrivateKey.Q.ToByteArrayUnsigned(),
                        DP = rsaPrivateKey.DP.ToByteArrayUnsigned(),
                        DQ = rsaPrivateKey.DQ.ToByteArrayUnsigned(),
                        InverseQ = rsaPrivateKey.QInv.ToByteArrayUnsigned(),
                        D = rsaPrivateKey.Exponent.ToByteArrayUnsigned()
                    };

                    rsa.ImportParameters(rsaParameters);
                    byte[] pkcs8PrivateKey = rsa.ExportRSAPrivateKey();
                    string pem = Convert.ToBase64String(pkcs8PrivateKey, Base64FormattingOptions.InsertLineBreaks);
                    pemPrivateKey = $"-----BEGIN PRIVATE KEY-----\n{pem}\n-----END PRIVATE KEY-----";
                    

                    /*StringWriter stringWriter = new StringWriter();
                    PemWriter pemWriter = new PemWriter(stringWriter);
                    pemWriter.WriteObject(privateKey);
                    pemWriter.Writer.Flush();
                    pemPrivateKey = stringWriter.ToString();
                    pemPrivateKey = pemPrivateKey
                        .Replace("-----BEGIN RSA PRIVATE KEY-----", "-----BEGIN PRIVATE KEY-----")
                        .Replace("-----END RSA PRIVATE KEY-----", "-----END PRIVATE KEY-----");*/

                    break;
                }
                else if (pkcs12Store.IsCertificateEntry(alias))
                {
                    X509CertificateEntry certEntry = pkcs12Store.GetCertificate(alias);
                    // Create an X509Certificate2 object from the certificate entry
                    X509Certificate2 certificate = new X509Certificate2(certEntry.Certificate.GetEncoded());

                    if (certificate.Subject == certificate.Issuer)
                    {
                        rootCertificatePem =
                            $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";
                    }
                }
                else
                {
                    _logger.LogDebug("PFX cert has no contents");
                }
            }

            VcenterCertificateManagementVcenterTlsSet certReq = new VcenterCertificateManagementVcenterTlsSet
            {
                cert = certificatePem,
                key = pemPrivateKey,
                root_cert = rootCertificatePem
            };

            VcenterClient.AddVcenterSslCertificate(certReq);
        }

        public static (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(
            X509Certificate2 cert)
        {
            // Convert the certificate to PEM format
            string certificatePem =
                $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";

            // Convert the private key to PEM format
            string privateKeyPem = ExportPrivateKeyToPem(cert);

            return (certificatePem, privateKeyPem);
        }

        public static X509Certificate2 GetRootCA(X509Certificate2 cert)
        {
            // Get the root CA from the certificate chain
            X509Chain chain = new X509Chain();
            chain.Build(cert);

            X509Certificate2 rootCA = null;
            foreach (X509ChainElement element in chain.ChainElements)
            {
                if (element.Certificate.Subject == element.Certificate.Issuer)
                {
                    rootCA = element.Certificate;
                    break;
                }
            }

            return rootCA;
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
    }
}
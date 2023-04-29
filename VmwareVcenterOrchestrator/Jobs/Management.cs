using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

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
                        
                        _logger.LogDebug("Add operation complete");

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

            X509Certificate2 certificate = new X509Certificate2(pkcs12CertBytes, config.JobCertificate.PrivateKeyPassword, X509KeyStorageFlags.Exportable);
            
            (string certificatePem, string privateKeyPem) = ConvertCertificateToPemStrings(certificate);

            string caCertificatePem = ExtractRootCAtoPemString(certificate);
            
            VcenterCertificateManagementVcenterTlsSet certReq = new VcenterCertificateManagementVcenterTlsSet
            {
                cert = certificatePem,
                key = privateKeyPem,
                root_cert = caCertificatePem
            };
            
            _logger.LogDebug("Adding certificate to vCenter");
            VcenterClient.ReplaceVcenterSslCertificate(certReq);
        }

        public static (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(
            X509Certificate2 cert)
        {
            // Convert the certificate to PEM format
            string certificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(cert.Export(X509ContentType.Cert))}\n-----END CERTIFICATE-----";

            // Convert the private key to PEM format
            string privateKeyPem = ExportPrivateKeyToPem(cert);

            return (certificatePem, privateKeyPem);
        }

        public string ExtractRootCAtoPemString(X509Certificate2 cert)
        {
            // Get the issuer's distinguished name (DN)
            string issuerDn = cert.Issuer;

            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            // Find the issuer certificate by DN
            X509Certificate2Collection issuerCertificates = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, issuerDn, false);
            string caCertificatePem = string.Empty;
            if (issuerCertificates.Count > 0)
            {
                X509Certificate2 issuerCertificate = issuerCertificates[0];
                caCertificatePem = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(issuerCertificate.Export(X509ContentType.Cert))}\n-----END CERTIFICATE-----";
            }
            else
            {
                _logger.LogDebug("The root CA information for {0} cannot be found.", cert.FriendlyName);
            }

            store.Close();
            
            return caCertificatePem;

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
    }
    
    
}
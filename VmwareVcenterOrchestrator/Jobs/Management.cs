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

using System.Linq.Expressions;
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

                        if (string.IsNullOrEmpty(config.JobCertificate.PrivateKeyPassword))
                        {
                            PerformTrustedRootAddition(config).Wait();
                        }
                        else
                        {
                            PerformSslReplacement(config).Wait();
                        }

                        _logger.LogDebug("Add operation complete");

                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Removing certificate from vCenter");

                        PerformRemove(config).Wait();

                        _logger.LogDebug("Remove operation complete.");

                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    default:
                        _logger.LogDebug("Invalid management operation type: {0}", config.OperationType);
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing job:\n {ex.Message}");
                result.FailureMessage = ex.Message;
            }

            return result;
        }

        private async Task PerformSslReplacement(ManagementJobConfiguration config)
        {
            byte[] pkcs12CertBytes = Convert.FromBase64String(config.JobCertificate.Contents);

            X509Certificate2 certificate = new(pkcs12CertBytes, config.JobCertificate.PrivateKeyPassword, X509KeyStorageFlags.Exportable);

            string privateKeyPem;

            if (certificate.HasPrivateKey)
            {
                _logger.LogTrace("Has private key, Extracting..");
                privateKeyPem = certificate.GetRSAPrivateKey().ExportPkcs8PrivateKeyPem();
            }
            else
            {
                _logger.LogError("No private key has been provided, but is required.");
                throw new Exception("No private key has been provided, but is required.");
            }

            // var privateKeyPem = certificate.PrivateKey.pem
            //certificate.TryExportCertificatePem(new string certificatePem, out charsWritten);

            //(string certificatePem, string privateKeyPem) = certificate.ExportCertificatePem();
            ConvertCertificateToPemStrings(certificate);
            var caRootCert = ExtractRootCA(certificate);

            if (caRootCert == null)
            {
                _logger.LogError("Unable to extract the root CA necessary to replace vCenter SSL Cert.");
                throw new Exception("Unable to extract the root CA necessary to replace vCenter SSL Cert.");
            }
            //string caCertificatePem = ExtractRootCAtoPemString(certificate);

            VcenterCertificateManagementVcenterTlsSet certReq = new VcenterCertificateManagementVcenterTlsSet
            {
                cert = certificate.ExportCertificatePem(),
                key = privateKeyPem,
                root_cert = caRootCert.ExportCertificatePem()
            };

            _logger.LogDebug("Adding certificate to vCenter");
            await VcenterClient.ReplaceVcenterSslCertificate(certReq);
        }

        private X509Certificate2? ExtractRootCA(X509Certificate2 cert)
        {
            var chain = new X509Chain();
            chain.Build(cert);

            // CA is always last element of a full CA chain.

            X509ChainElement chainElement = chain.ChainElements[chain.ChainElements.Count - 1];

            foreach (X509ChainStatus status in chainElement.ChainElementStatus)
            {
                if (status.Status == X509ChainStatusFlags.PartialChain)
                {
                    return null;
                }
            }

            return chainElement.Certificate;


            //X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            //store.Open(OpenFlags.ReadOnly);

            //// Find the issuer certificate by DN
            //X509Certificate2Collection issuerCertificates = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, issuerDn, false);

            //var chain = store.
        }

        private async Task PerformTrustedRootAddition(ManagementJobConfiguration config)
        {
            VcenterCertificateManagementX509CertChain certContents = new VcenterCertificateManagementX509CertChain
            {
                cert_chain = new List<string> { $"-----BEGIN CERTIFICATE-----\n{config.JobCertificate.Contents}\n-----END CERTIFICATE-----" }
            };

            VcenterCertificateManagementVcenterTrustedRootChainsCreate req = new VcenterCertificateManagementVcenterTrustedRootChainsCreate
            {
                cert_chain = certContents,
            };

            await VcenterClient.AddVcenterTrustedRoot(req);
        }

        public (string CertificatePem, string PrivateKeyPem) ConvertCertificateToPemStrings(
            X509Certificate2 cert)
        {
            //cert.pem
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

        private string ExportPrivateKeyToPem(X509Certificate2 certificate)
        {
            //AsymmetricAlgorithm privateKey = certificate.GetRSAPrivateKey();
            _logger.LogTrace("Extracting PEM formatted private key");
            string pkPem = certificate.PrivateKey.ExportPkcs8PrivateKeyPem();
            if (string.IsNullOrEmpty(pkPem))
            {
                throw new NotSupportedException("Unsupported private key algorithm");
            }
            return pkPem;

            //if (privateKey is RSA or ECDsa)
            //{
            //    byte[] pkcs8PrivateKey = certificate.PrivateKey.ExportPkcs8PrivateKey();
            //    string pem = Convert.ToBase64String(pkcs8PrivateKey);
            //    return $"-----BEGIN PRIVATE KEY-----\n{pem}\n-----END PRIVATE KEY-----";
            //}

            // Add support for other key types if needed            
        }

        public async Task PerformRemove(ManagementJobConfiguration config)
        {
            _logger.LogTrace("starting management > remove task");
            //retrieve the trusted root information

            try
            {
                var trustedRootChains = await VcenterClient.GetVcenterTrustedRootChains();

                _logger.LogTrace("received trusted root chain response.");

                foreach (string trustedRootChain in trustedRootChains)
                {
                    VcenterCertificateManagementVcenterTrustedRootChainsInfo trustedRootInfo = await VcenterClient.GetVcenterTrustedRootChain(trustedRootChain);
                    _logger.LogTrace("Formatting the response.");
                    //Format the retrieved trusted root chain certificate
                    //Remove X509 CRL Cert if it exists
                    string trimPoint = "\n-----END CERTIFICATE-----";
                    int index = trustedRootInfo.cert_chain.cert_chain[0].IndexOf(trimPoint);
                    string trustedRootCert = string.Empty;
                    if (index >= 0)
                    {
                        trustedRootCert = trustedRootInfo.cert_chain.cert_chain[0].Substring(0, index);
                    }
                    byte[] pkcs12CertBytes = Convert.FromBase64String(trustedRootCert.TrimStart("-----BEGIN CERTIFICATE-----\n".ToCharArray()));
                    X509Certificate2 certificate = new X509Certificate2(pkcs12CertBytes);

                    // Extract the CN from the subject name for alias
                    //string name = string.Empty;
                    //string[] subjectDn = certificate.SubjectName.Name.Split(',');
                    //for (int i = 0; i < subjectDn.Length; i++)
                    //{
                    //    if (subjectDn[i].Contains("CN="))
                    //    {
                    //        name = subjectDn[i].Trim().Substring("CN=".Length);
                    //        break;
                    //    } 
                    //    if (i == subjectDn.Length - 1)
                    //    {
                    //        name = certificate.SubjectName.Name;
                    //    }
                    //}

                    //check if the trusted root alias matches the job alias
                    if (certificate.Thumbprint == config.JobCertificate.Alias)
                    {
                        await VcenterClient.RemoveVcenterTrustedRoot(trustedRootChain);
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Removal job failed with exception message: `{ex.Message}`");
                throw;
            }
        }
    }
}
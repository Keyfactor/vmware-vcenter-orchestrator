
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    public class Management : VmwareVcenterJob<Management>, IManagementJobExtension
    {
        public Management(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            Initialize(config.CertificateStoreDetails);
            _logger.LogDebug("Beginning Vmware Vcenter Management Job");

            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };
            var configJson = System.Text.Json.JsonSerializer.Serialize(config);
            switch (config.OperationType)
            {
                case CertStoreOperationType.Add:
                    _logger.LogDebug("Adding certificate to Vcenter");
                    _logger.LogTrace($"config values: {configJson}");
                    if (string.IsNullOrEmpty(config.JobCertificate.PrivateKeyPassword))
                    {
                        _logger.LogTrace("No Private Key Password included. Adding as trusted root certificate");
                        try
                        {
                            PerformTrustedRootAddition(config).Wait();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error adding trusted root. {ex.Message}");
                            result.FailureMessage = ex.Message;
                            return result;
                        }
                    }
                    else
                    {
                        _logger.LogTrace("Private Key Password is included. Adding as TLS certificate.");
                        try
                        {
                            PerformSslReplacement(config).Wait();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error updating TLS certificate. {ex.Message}");
                            result.FailureMessage = ex.Message;
                            return result;
                        }
                    }

                    _logger.LogDebug("Add operation complete");
                    result.Result = OrchestratorJobStatusJobResult.Success;
                    break;

                case CertStoreOperationType.Remove:
                    _logger.LogDebug("Removing certificate from vCenter");

                    try
                    {
                        PerformRemove(config).Wait();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error removing certificate from vCenter. {ex.Message}");
                        result.FailureMessage = ex.Message;
                        return result;
                    }

                    _logger.LogDebug("Remove operation complete.");

                    result.Result = OrchestratorJobStatusJobResult.Success;
                    break;
                default:
                    _logger.LogDebug($"Invalid operation type: {config.OperationType}");
                    throw new ArgumentOutOfRangeException($"Invalid operation type: {config.OperationType}");
            }

            return result;
        }


        private async Task PerformSslReplacement(ManagementJobConfiguration config)
        {
            byte[] pkcs12CertBytes = Convert.FromBase64String(config.JobCertificate.Contents);

            X509Certificate2 certificate = new(pkcs12CertBytes, config.JobCertificate.PrivateKeyPassword, X509KeyStorageFlags.Exportable);

            var caRootCertPem = certificate.ExportCARootPem(_logger);

            _logger.LogTrace($"ca root PEM: \n\n{caRootCertPem}\n\n");

            if (string.IsNullOrEmpty(caRootCertPem))
            {
                _logger.LogError("Unable to extract the root CA necessary to replace vCenter SSL Cert.");
                throw new Exception("Unable to extract the root CA necessary to replace vCenter SSL Cert.");
            }

            (var certPem, var keyPem) = certificate.ExportCertAndPrivateKeyPem();
            _logger.LogTrace($"ca root PEM: \n\n{caRootCertPem}\n\n");


            VCenterTlsCertSet certReq = new VCenterTlsCertSet
            {
                cert = certPem,
                key = keyPem,
                root_cert = caRootCertPem
            };
            var jsonReq = JsonSerializer.Serialize(certReq);

            _logger.LogTrace($"TLS Cert Set request Payload: \n\n {jsonReq} \n\n");

            _logger.LogDebug("Adding certificate to vCenter");
            await VcenterClient.ReplaceVcenterSslCertificate(certReq);
        }

        private async Task PerformTrustedRootAddition(ManagementJobConfiguration config)
        {
            var certContents = new VCenterX509CertChain
            {
                cert_chain = new List<string> { $"{X509Certificate2Extensions.CERTIFICATE_HEADER_PEM}{config.JobCertificate.Contents}{X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM}" }
            };

            var req = new VCenterTrustedRootChainsCreate
            {
                cert_chain = certContents,
            };

            await VcenterClient.AddTrustedRoot(req);
        }

        public async Task PerformRemove(ManagementJobConfiguration config)
        {
            _logger.LogTrace("starting management > remove task");

            try
            {
                //retrieve the trusted root information
                var trustedRootChains = await VcenterClient.GetTrustedRootChains();
                _logger.LogTrace("received trusted root chain response.");

                foreach (string trustedRootChain in trustedRootChains)
                {
                    var trustedRootInfo = await VcenterClient.GetTrustedRootChain(trustedRootChain);
                    _logger.LogTrace("Formatting the response.");
                    //Format the retrieved trusted root chain certificate
                    //Remove X509 CRL Cert if it exists
                    var index = trustedRootInfo.cert_chain.cert_chain[0].IndexOf(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
                    var trustedRootCert = string.Empty;
                    if (index >= 0)
                    {
                        trustedRootCert = trustedRootInfo.cert_chain.cert_chain[0].Substring(0, index);
                    }
                    var pkcs12CertBytes = Convert.FromBase64String(trustedRootCert.TrimStart(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM.ToCharArray()));
                    var certificate = new X509Certificate2(pkcs12CertBytes);

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

//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    [Job("Inventory")]
    public class Inventory : VmwareVcenterJob<Inventory>, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
        {
            _logger.LogDebug($"Beginning VMware vCenter Inventory Job");

            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            Initialize(config.CertificateStoreDetails);

            List<CurrentInventoryItem> inventoryItems;

            try
            {
                //inventory ssl certificate and trusted root certificates
                _logger.LogTrace("adding the SSL cert to the inventory..");
                inventoryItems = FormatSslCert(VcenterClient.GetVcenterSslCertificate().GetAwaiter().GetResult()).ToList();
                _logger.LogTrace("successfully added the SSL cert to the inventory");

                _logger.LogTrace("retrieving the trusted root chains");
                var trustedRootChains = VcenterClient.GetTrustedRootChains().GetAwaiter().GetResult();

                foreach (string trustedRootChain in trustedRootChains)
                {
                    _logger.LogTrace($"formatting the trusted root: {trustedRootChain}");
                    CurrentInventoryItem trustedRootInventoryItem = FormatTrustedRoot(VcenterClient.GetTrustedRootChain(trustedRootChain).Result);
                    inventoryItems.Add(trustedRootInventoryItem);
                }

            }
            catch (Exception ex)
            {
                var errMsg = "Error getting vCenter SSL Certificate:\n" + ex.Message;
                _logger.LogError(errMsg);
                result.FailureMessage = errMsg; 
                return result;
            }

            _logger.LogDebug($"Found {inventoryItems.Count} certificate(s) in vCenter");

            cb.DynamicInvoke(inventoryItems);

            result.Result = OrchestratorJobStatusJobResult.Success;
            return result;
        }

        public IEnumerable<CurrentInventoryItem> FormatSslCert(VCenterTlsCertInfo sslCert)
        {
            _logger.MethodEntry();
            var inventoryItems = new List<CurrentInventoryItem>();

            // vCenter certs are in PEM format
            // Remove the BEGIN/END
            sslCert.cert = sslCert.cert.Replace(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM, string.Empty).Replace(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM, string.Empty);
            
            // Create new inventory item for the certificate

            var inventoryItem = new CurrentInventoryItem()
            {
                Alias = sslCert.thumbprint,
                PrivateKeyEntry = true,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = new string[] { sslCert.cert }
            };
            inventoryItems.Add(inventoryItem);
            return inventoryItems;
        }

        public CurrentInventoryItem FormatTrustedRoot(VCenterTrustedRootChainsInfo trustedRootInfo)
        {
            _logger.MethodEntry();
            //Format the retrieved trusted root chain certificate
            //Remove attached X509 CRL Cert if it exists            
            var index = trustedRootInfo.cert_chain.cert_chain[0].IndexOf(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
            var trustedRootCert = string.Empty;
            if (index >= 0)
            {
                trustedRootCert = trustedRootInfo.cert_chain.cert_chain[0].Substring(0, index);
            }
            var pkcs12CertBytes = Convert.FromBase64String(trustedRootCert.TrimStart(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM.ToCharArray()));
            var certificate = new X509Certificate2(pkcs12CertBytes);

            // Create new inventory item for the certificate
            var certList = new List<string> { Convert.ToBase64String(certificate.RawData) };

            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = certificate.Thumbprint,
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = certList
            };
            return inventoryItem;
        }
    }
}
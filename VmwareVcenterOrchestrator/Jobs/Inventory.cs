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

using System.Security.Cryptography.X509Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    [Job("Inventory")]
    public class Inventory : VmwareVcenterJob<Inventory>, IInventoryJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Inventory>();

        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
        {
            _logger.LogDebug($"Beginning VMware vCenter Inventory Job");

            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            Initialize(config.CertificateStoreDetails);

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                inventoryItems = FormatSslCert(VcenterClient.GetVcenterSslCertificate()).ToList();
                List<string> trustedRootChains = VcenterClient.GetVcenterTrustedRootChains();
                foreach (string trustedRootChain in trustedRootChains)
                {
                    CurrentInventoryItem trustedRootInventoryItem = FormatTrustedRoot(VcenterClient.GetVcenterTrustedRootChain(trustedRootChain));
                    inventoryItems.Add(trustedRootInventoryItem);
                }

            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vCenter SSL Certificate:\n" + ex.Message);
                result.FailureMessage = "Error getting vCenter SSL Certificate:\n" + ex.Message;
                return result;
            }
            
            _logger.LogDebug($"Found {inventoryItems.Count} certificate(s) in vCenter");
            
            cb.DynamicInvoke(inventoryItems);
            
            result.Result = OrchestratorJobStatusJobResult.Success;
            return result;
        }

        public IEnumerable<CurrentInventoryItem> FormatSslCert(VcenterCertificateManagementVcenterTlsInfo SslCert)
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            
            // Vcenter certs are in PEM format
            //Remove the BEGIN/END
            SslCert.cert = SslCert.cert.Replace("-----BEGIN CERTIFICATE-----\n", string.Empty).Replace("\n-----END CERTIFICATE-----", string.Empty);
           
            // Create new inventory item for the certificate
            List<string> certList = new List<string>{ SslCert.cert };
            
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
        
        public CurrentInventoryItem FormatTrustedRoot(VcenterCertificateManagementVcenterTrustedRootChainsInfo trustedRootInfo)
        {
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
                
            // Extract the CN
            string cn = certificate.SubjectName.Name.Split(',')[0].TrimStart("CN=".ToCharArray());

            // Create new inventory item for the certificate
            List<string> certList = new List<string>{ Convert.ToBase64String(certificate.RawData) };
            
            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = cn, 
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = certList
            };
            return inventoryItem;
        }
    }
}
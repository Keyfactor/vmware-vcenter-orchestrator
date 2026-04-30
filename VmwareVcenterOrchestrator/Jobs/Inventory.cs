
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
    public class Inventory : VmwareVcenterJob, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
        {
            Initialize(config);

            _logger.LogDebug($"Beginning VMware vCenter Inventory Job");

            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            List<CurrentInventoryItem> inventoryItems;

            try
            {
                //inventory ssl certificate and trusted root certificates
                _logger.LogTrace("adding the SSL cert to the inventory..");
                inventoryItems = FormatSslCert(VcenterClient.GetVcenterSslCertificate().GetAwaiter().GetResult())?.ToList();
                _logger.LogTrace("successfully added the SSL cert to the inventory");

                _logger.LogTrace("retrieving the trusted root chains");
                var trustedRootChains = VcenterClient.GetTrustedRootChains().GetAwaiter().GetResult();

                foreach (string trustedRootChain in trustedRootChains)
                {
                    _logger.LogTrace($"retreiving the trusted root with SN: {trustedRootChain}");
                    var trustedRootCerts = VcenterClient.GetTrustedRootChain(trustedRootChain).GetAwaiter().GetResult();
                    CurrentInventoryItem trustedRootInventoryItem = FormatTrustedRoot(trustedRootCerts);
                    if (trustedRootInventoryItem != null) inventoryItems.Add(trustedRootInventoryItem);
                }

            }
            catch (Exception ex)
            {
                var errMsg = "An error occurred during the inventory job:\n" + ex.Message;
                _logger.LogError(LogHandler.FlattenException(ex));
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
            _logger.LogTrace($"trusted root chain: {String.Join(",", trustedRootInfo?.cert_chain?.cert_chain ?? Enumerable.Empty<string>())}");

            // Guard: null input, missing cert_chain object, or empty chain list.
            // The original condition used nullable-propagation logic that evaluated
            // to false (not true) when cert_chain was null, causing a NullReferenceException
            // on the very next line. Replaced with explicit null checks.
            if (trustedRootInfo == null
                || trustedRootInfo.cert_chain == null
                || trustedRootInfo.cert_chain.cert_chain == null
                || !trustedRootInfo.cert_chain.cert_chain.Any())
            {
                _logger.LogTrace("no entries found.");
                return null;
            }

            var rootCert = trustedRootInfo.cert_chain.cert_chain[0];

            var headerStartIndex = rootCert.IndexOf(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM);
            if (headerStartIndex == -1)
            {
                _logger.LogTrace("no PEM header found");
                return null;
            }

            var footerStartIndex = rootCert.IndexOf(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
            if (footerStartIndex == -1)
            {
                _logger.LogTrace("no PEM footer found");
                return null;
            }

            // Extract the raw base64 body: everything between the end of the header
            // and the start of the footer.  Then strip ALL whitespace so that
            // Convert.FromBase64String receives a clean string regardless of:
            //   - line wrap width (64-char, 76-char, or no wrapping)
            //   - line endings (LF, CRLF, or mixed)
            //   - leading/trailing spaces or blank lines in the body
            //   - bag-attribute blocks before the header (skipped by headerStartIndex)
            var bodyStartIndex = headerStartIndex + X509Certificate2Extensions.CERTIFICATE_HEADER_PEM.Length;
            var certContent = rootCert.Substring(bodyStartIndex, footerStartIndex - bodyStartIndex);
            certContent = new string(certContent.Where(c => !char.IsWhiteSpace(c)).ToArray());

            _logger.LogTrace("extracted cert content to be base64 decoded:");
            _logger.LogTrace(certContent);

            var pkcs12CertBytes = Convert.FromBase64String(certContent);

            _logger.LogTrace($"successfully decoded into a byte array of length {pkcs12CertBytes.Length}");

            _logger.LogTrace($"creating new x509 certificate from certificate byte array");
            var certificate = new X509Certificate2(pkcs12CertBytes);

            // Create new inventory item for the certificate
            var certList = new List<string> { Convert.ToBase64String(certificate.RawData) };

            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = certificate.Thumbprint,
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = certList,
            };
            return inventoryItem;
        }
    }
}
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

using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client
{
    public class VmwareVcenterClient
    {
        const string APITOKENENDPOINT = "/api/session";
        const string TLSCERTENDPOINT = "/api/vcenter/certificate-management/vcenter/tls";
        const string TRUSTEDROOTENDPOINT = "/api/vcenter/certificate-management/vcenter/trusted-root-chains/";
        private ILogger _logger { get; }
        private HttpClient VcenterClient { get; }

        public VmwareVcenterClient(string vCenterHostname, string username, string password)
        {
            _logger = LogHandler.GetClassLogger<VmwareVcenterClient>();
            _logger.LogDebug("Initializing VMware vCenter client");

            var VcenterClientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            VcenterClient = new HttpClient(VcenterClientHandler)
            {
                BaseAddress = new Uri("https://" + vCenterHostname)
            };

            var apiKey = GetApiToken(username, password).Result;
            VcenterClient.DefaultRequestHeaders.Add("vmware-api-session-id", apiKey);
        }

        private async Task<string?> GetApiToken(string username, string password)
        {
            var credentials = username + ":" + password;
            var encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            
            var request = new HttpRequestMessage(HttpMethod.Post, APITOKENENDPOINT);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            
            _logger.LogDebug("Calling POST on vcenter endpoint for TLS certificates", request);

            var response = await VcenterClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage.ToString());
            }

            var apiKey = await response.Content.ReadAsStringAsync();
                        
            return apiKey.Trim('\"');
        }

        public async Task<VCenterTlsCertInfo> GetVcenterSslCertificate() 
        {
            //This endpoint does not return certificate chains
            _logger.LogDebug("Calling GET on vcenter endpoint for TLS certificates", TLSCERTENDPOINT);

            var response = await VcenterClient.GetAsync(TLSCERTENDPOINT);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage.ToString());
            }

            var sslCertResp = await response.Content.ReadAsStringAsync();            
            var SslCert = JsonConvert.DeserializeObject<VCenterTlsCertInfo>(sslCertResp);
            
            return SslCert;
        }

        public async Task ReplaceVcenterSslCertificate(VCenterTlsCertSet cert)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(cert);
            var request_body = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            _logger.LogDebug($"Calling PUT on vcenter endpoint for TLS certificates: {request_body}");
            
            var response = await VcenterClient.PutAsync(TLSCERTENDPOINT, request_body);

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }            
        }
        
        public async Task RemoveVcenterTrustedRoot(string chain)
        {
            var request_uri = TRUSTEDROOTENDPOINT + chain;
            _logger.LogDebug($"Calling DELETE on vcenter endpoint for trusted root chain: {request_uri}");
            var response = await VcenterClient.DeleteAsync(request_uri);

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage);
            }            
        }
        
        public async Task<List<string>> GetTrustedRootChains()
        {
            _logger.LogDebug("Calling GET on vcenter endpoint for trusted root chain");
            var response = await VcenterClient.GetAsync(TRUSTEDROOTENDPOINT);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            var trustedRoots = JsonConvert.DeserializeObject<List<VCenterTrustedRootChainsSummary>>(responseContent);
            var chains = trustedRoots.Select(tr => tr.chain).ToList();
            
            return chains;
        }

        public async Task<VCenterTrustedRootChainsInfo> GetTrustedRootChain(string chain)
        {
            var request_uri = TRUSTEDROOTENDPOINT + chain;            
            _logger.LogDebug("Calling GET on vcenter endpoint for trusted root chain", request_uri);

            var response = await VcenterClient.GetAsync(request_uri);

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage);
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();            
            var trustedRootInfo = JsonConvert.DeserializeObject<VCenterTrustedRootChainsInfo>(responseContent);
            return trustedRootInfo;
        }
        
        public async Task AddTrustedRoot(VCenterTrustedRootChainsCreate trustedRootChain)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(trustedRootChain);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            _logger.LogDebug("Calling POST on vcenter endpoint for trusted root chain", request);

            var response = await VcenterClient.PostAsync(TRUSTEDROOTENDPOINT, request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }
        }
    }
}
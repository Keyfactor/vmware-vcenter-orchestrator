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
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client
{
    public class VmwareVcenterClient
    {
        const string TLSCERTENDPOINT = "/api/vcenter/certificate-management/vcenter/tls";
        const string TRUSTEDROOTENDPOINT = "/api/vcenter/certificate-management/vcenter/trusted-root-chains/";


        public VmwareVcenterClient(string vCenterHostname, string username, string password)
        {
            Log = LogHandler.GetClassLogger<VmwareVcenterClient>();
            Log.LogDebug("Initializing VMware vCenter client");

            var VcenterClientHandler = new HttpClientHandler();

            VcenterClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            VcenterClient = new HttpClient(VcenterClientHandler)
            {
                BaseAddress = new Uri("https://" + vCenterHostname)
            };

            var apiKey = GetApiToken(username, password).Result;
            VcenterClient.DefaultRequestHeaders.Add("vmware-api-session-id", apiKey);
        }
        private ILogger Log { get; }
        private HttpClient VcenterClient { get; }

        private async Task<string?> GetApiToken(string username, string password)
        {
            string credentials = username + ":" + password;
            string encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            
            Log.LogDebug("Calling POST on vcenter endpoint for TLS certificates", request);

            var response = await VcenterClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage.ToString());
            }

            var apiKeyTask = await response.Content.ReadAsStringAsync();
                        
            return apiKeyTask?.Trim('\"');
        }

        public async Task<VcenterCertificateManagementVcenterTlsInfo> GetVcenterSslCertificate() 
        {
            //This endpoint does not return certificate chains
            Log.LogDebug("Calling GET on vcenter endpoint for TLS certificates", TLSCERTENDPOINT);
            var response = await VcenterClient.GetAsync(TLSCERTENDPOINT);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage.ToString());
            }

            var sslCertResp = await response.Content.ReadAsStringAsync();            
            var SslCert = JsonConvert.DeserializeObject<VcenterCertificateManagementVcenterTlsInfo>(sslCertResp);
            
            return SslCert;
        }

        public async Task ReplaceVcenterSslCertificate(VcenterCertificateManagementVcenterTlsSet cert)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(cert);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            Log.LogDebug("Calling PUT on vcenter endpoint for TLS certificates", request);
            
            var response = await VcenterClient.PutAsync(TLSCERTENDPOINT, request);
                        
            //give the server time to update with the new certificate before checking for success
            //Task.Delay(TimeSpan.FromMinutes(3)).Wait();

            //parse status code for error handling

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }            
        }
        
        public async Task RemoveVcenterTrustedRoot(string chain)
        {
            string request = TRUSTEDROOTENDPOINT + chain;
            Log.LogDebug("Calling DELETE on vcenter endpoint for trusted root chain", request);
            var response = await VcenterClient.DeleteAsync(request);

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage);
            }            
        }
        
        public async Task<List<string>> GetVcenterTrustedRootChains()
        {
            Log.LogDebug("Calling GET on vcenter endpoint for trusted root chain");
            var response = await VcenterClient.GetAsync(TRUSTEDROOTENDPOINT);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            var trustedRoots = JsonConvert.DeserializeObject<List<VcenterCertificateManagementVcenterTrustedRootChainsSummary>>(responseContent);
            var chains = trustedRoots.Select(tr => tr.chain).ToList();
            
            return chains;
        }

        public async Task<VcenterCertificateManagementVcenterTrustedRootChainsInfo> GetVcenterTrustedRootChain(string chain)
        {
            string request = TRUSTEDROOTENDPOINT + chain;
            Log.LogDebug("Calling GET on vcenter endpoint for trusted root chain", request);
            var response = await VcenterClient.GetAsync(request);

            if (!response.IsSuccessStatusCode) {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception(errorMessage);
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();            
            VcenterCertificateManagementVcenterTrustedRootChainsInfo trustedRootInfo = JsonConvert.DeserializeObject<VcenterCertificateManagementVcenterTrustedRootChainsInfo>(responseContent);
            return trustedRootInfo;
        }
        
        public async Task AddVcenterTrustedRoot(VcenterCertificateManagementVcenterTrustedRootChainsCreate trustedRootChain)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(trustedRootChain);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");

            Log.LogDebug("Calling POST on vcenter endpoint for trusted root chain", request);
            var response = await VcenterClient.PostAsync(TRUSTEDROOTENDPOINT, request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();                
                throw new Exception(errorMessage);
            }
        }
    }
}
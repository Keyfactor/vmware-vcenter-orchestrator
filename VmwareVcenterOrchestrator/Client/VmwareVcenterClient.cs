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
        public VmwareVcenterClient(string vCenterHostname, string username, string password)
        {
            Log = LogHandler.GetClassLogger<VmwareVcenterClient>();
            Log.LogDebug("Initializing VMware vCenter client");

            var VcenterClientHandler = new HttpClientHandler();

            VcenterClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            VcenterClient = new HttpClient(VcenterClientHandler);
            VcenterClient.BaseAddress = new Uri("https://" + vCenterHostname);

            var apiKey = GetApiToken(username, password);
            VcenterClient.DefaultRequestHeaders.Add("vmware-api-session-id", apiKey);
        }
        private ILogger Log { get; }
        private HttpClient VcenterClient { get; }

        private string GetApiToken(string username, string password)
        {
            string credentials = username + ":" + password;
            string encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
            
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            var response = VcenterClient.SendAsync(request);
            response.Wait();
            
            var apiKeyTask = response.Result.Content.ReadAsStringAsync();
            apiKeyTask.Wait();
            
            return apiKeyTask.Result.Trim('\"');
        }

        public VcenterCertificateManagementVcenterTlsInfo GetVcenterSslCertificate() 
        {
            //This endpoint does not return certificate chains
            var response = VcenterClient.GetAsync("/api/vcenter/certificate-management/vcenter/tls");
            response.Wait();
            var sslCertResp = response.Result.Content.ReadAsStringAsync();
            sslCertResp.Wait();
            var SslCert = JsonConvert.DeserializeObject<VcenterCertificateManagementVcenterTlsInfo>(sslCertResp.Result);
            
            return SslCert;
        }

        public void ReplaceVcenterSslCertificate(VcenterCertificateManagementVcenterTlsSet cert)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(cert);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            var response = VcenterClient.PutAsync("/api/vcenter/certificate-management/vcenter/tls", request);
            response.Wait();
            
            //give the server time to update with the new certificate before checking for success
            Task.Delay(TimeSpan.FromMinutes(3)).Wait();
            
            //parse status code for error handling
            string statusCode = string.Empty;
            string[] respMessage = response.Result.ToString().Split(',');
            for (int i = 0; i < respMessage.Length; i++)
            {
                if (respMessage[i].Contains("StatusCode:"))
                {
                    statusCode = respMessage[i].Trim().Substring("StatsCode: ".Length).Trim();
                    break;
                }
            }
            
            if (statusCode != "204")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }
        }
        
        public void RemoveVcenterTrustedRoot(string chain)
        {
            string request = "/api/vcenter/certificate-management/vcenter/trusted-root-chains/" + chain;
            var response = VcenterClient.DeleteAsync(request);
            response.Wait();
            
            //parse status code for error handling
            string statusCode = string.Empty;
            string[] respMessage = response.Result.ToString().Split(',');
            for (int i = 0; i < respMessage.Length; i++)
            {
                if (respMessage[i].Contains("StatusCode:"))
                {
                    statusCode = respMessage[i].Trim().Substring("StatsCode: ".Length).Trim();
                    break;
                }
            }
            
            if (statusCode != "204")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }
        }
        
        public List<string> GetVcenterTrustedRootChains()
        {
            var response = VcenterClient.GetAsync("/api/vcenter/certificate-management/vcenter/trusted-root-chains/");
            response.Wait();
            if (response.Result.StatusCode.ToString() != "OK")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }

            var responseContent = response.Result.Content.ReadAsStringAsync();
            responseContent.Wait();
            List<VcenterCertificateManagementVcenterTrustedRootChainsSummary> trustedRoots = JsonConvert.DeserializeObject<List<VcenterCertificateManagementVcenterTrustedRootChainsSummary>>(responseContent.Result);
            List<string> chains = new List<string>();
            foreach (VcenterCertificateManagementVcenterTrustedRootChainsSummary trustedRoot in trustedRoots)
            {
                chains.Add(trustedRoot.chain);
            }

            return chains;
        }

        public VcenterCertificateManagementVcenterTrustedRootChainsInfo GetVcenterTrustedRootChain(string chain)
        {
            string request = "/api/vcenter/certificate-management/vcenter/trusted-root-chains/" + chain;
            var response = VcenterClient.GetAsync(request);
            response.Wait();
            if (response.Result.StatusCode.ToString() != "OK")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }
            
            var responseContent = response.Result.Content.ReadAsStringAsync();
            responseContent.Wait();
            VcenterCertificateManagementVcenterTrustedRootChainsInfo trustedRootInfo = JsonConvert.DeserializeObject<VcenterCertificateManagementVcenterTrustedRootChainsInfo>(responseContent.Result);
            return trustedRootInfo;
        }
        
        public void AddVcenterTrustedRoot(VcenterCertificateManagementVcenterTrustedRootChainsCreate trustedRootChain)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(trustedRootChain);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            var response = VcenterClient.PostAsync("/api/vcenter/certificate-management/vcenter/trusted-root-chains", request);
            response.Wait();
            if (response.Result.StatusCode.ToString() != "Created")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }
        }
    }
}
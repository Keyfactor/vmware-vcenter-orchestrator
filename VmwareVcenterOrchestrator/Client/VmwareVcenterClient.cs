using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using System.Net.Http.Headers;
using System.Text;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

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

        public IEnumerable<CurrentInventoryItem> GetVcenterSslCertificate() 
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            //This endpoint does not return certificate chains
            var response = VcenterClient.GetAsync("/api/vcenter/certificate-management/vcenter/tls");
            response.Wait();
            var sslCertResp = response.Result.Content.ReadAsStringAsync();
            sslCertResp.Wait();
            var SslCert = JsonConvert.DeserializeObject<VcenterCertificateManagementVcenterTlsInfo>(sslCertResp.Result);
            
            // Vcenter certs are in PEM format
            //Remove the BEGIN/END
            SslCert.cert = SslCert.cert.Replace("-----BEGIN CERTIFICATE-----\n", string.Empty).Replace("\n-----END CERTIFICATE-----", string.Empty);
           
            // Create new inventory item for the certificate
            List<string> certList = new List<string>{ SslCert.cert };
            
            CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
            {
                Alias = SslCert.subject_alternative_name[0],
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = certList
            };
            inventoryItems.Add(inventoryItem);

            return inventoryItems;
        }

        public void ReplaceVcenterSslCertificate(VcenterCertificateManagementVcenterTlsSet cert)
        {
            var jsonTrustedRootChain = JsonConvert.SerializeObject(cert);
            var request = new StringContent(jsonTrustedRootChain, Encoding.UTF8, "application/json");
            var response = VcenterClient.PutAsync("/api/vcenter/certificate-management/vcenter/tls", request);
            response.Wait();
            if (response.Result.StatusCode.ToString() != "204")
            {
                var errorMessage = response.Result.Content.ReadAsStringAsync();
                errorMessage.Wait();
                throw new Exception(errorMessage.ToString());
            }
        }
        
    }
}
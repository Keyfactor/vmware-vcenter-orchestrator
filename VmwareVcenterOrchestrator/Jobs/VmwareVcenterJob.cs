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

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    public abstract class VmwareVcenterJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "Vcenter";

        protected VmwareVcenterClient VcenterClient { get; private set; }

        protected void Initialize(CertificateStore details)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Certificate Store Configuration: {JsonConvert.SerializeObject(details)}");
            logger.LogDebug("Initializing VmwareVsphereClient");
            dynamic? properties = JsonConvert.DeserializeObject(details.Properties);

            string ClientMachine = details.ClientMachine;
            string Username = properties?.ServerUsername;
            string Password = properties?.ServerPassword;

            VcenterClient = new VmwareVcenterClient(ClientMachine, Username, Password);
            
        }
    }
}
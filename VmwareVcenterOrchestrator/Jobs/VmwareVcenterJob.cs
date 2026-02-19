
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;
using VmwareVcenterOrchestrator;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs
{
    public abstract class VmwareVcenterJob : IOrchestratorJobExtension
    {
        public string ExtensionName => "Vcenter";
        internal protected ILogger _logger { get; set; }

        protected VmwareVcenterClient VcenterClient { get; private set; }

        internal protected IPAMSecretResolver PamSecretResolver { get; set; }

        public VmwareVcenterJob(IPAMSecretResolver resolver)
        {
            PamSecretResolver = resolver;
        }

        protected void Initialize(InventoryJobConfiguration config)
        {
            _logger = LogHandler.GetClassLogger(GetType());
            LogPluginVersion();
            _logger.LogTrace($"Certificate Store Configuration: {JsonConvert.SerializeObject(config.CertificateStoreDetails)}");
            _logger.LogDebug("Initializing VmwareVsphereClient");

            VcenterProperties properties = JsonConvert.DeserializeObject<VcenterProperties>(config.CertificateStoreDetails?.Properties);
            
            _logger.LogTrace($"server username: {config.ServerUsername}");
            _logger.LogTrace($"server password: {config.ServerPassword}");
            _logger.LogTrace($"PamSecretResolver is {(PamSecretResolver == null ? "" : "not")} null");

            string ClientMachine = config.CertificateStoreDetails?.ClientMachine;
            string Username = PamUtilities.ResolvePAMField(PamSecretResolver, _logger, "Server Username", config.ServerUsername);
            string Password = PamUtilities.ResolvePAMField(PamSecretResolver, _logger, "Server Password", config.ServerPassword);

            VcenterClient = new VmwareVcenterClient(ClientMachine, Username, Password);
        }

        protected void Initialize(ManagementJobConfiguration config)
        {
            _logger = LogHandler.GetClassLogger(GetType());
            LogPluginVersion();
            _logger.LogTrace($"Certificate Store Configuration: {JsonConvert.SerializeObject(config.CertificateStoreDetails)}");
            _logger.LogDebug("Initializing VmwareVsphereClient");

            VcenterProperties properties = JsonConvert.DeserializeObject<VcenterProperties>(config.CertificateStoreDetails?.Properties);

            string ClientMachine = config.CertificateStoreDetails?.ClientMachine;
            _logger.LogTrace($"server username: {config.ServerUsername}");
            _logger.LogTrace($"server password: {config.ServerPassword}");
            string Username = PamUtilities.ResolvePAMField(PamSecretResolver, _logger, "Server Username", config.ServerUsername);
            string Password = PamUtilities.ResolvePAMField(PamSecretResolver, _logger, "Server Password", config.ServerPassword);

            VcenterClient = new VmwareVcenterClient(ClientMachine, Username, Password);
        }

        protected void LogPluginVersion()
        {
            var targetAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = targetAssembly?.GetName();
            var version = assemblyName?.Version;
            _logger.LogTrace("Keyfactor Orchestrator Extension for VMWare VCenter");
            _logger.LogTrace($"{assemblyName?.Name ?? "unknown"} v{version}");
        }
    }
}
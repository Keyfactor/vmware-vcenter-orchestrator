
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
#nullable enable
public class VCenterTlsCertInfo
{
    public string issuer_dn { get; set; }
    public List<string> key_usage { get; set; }
    public string thumbprint { get; set; }
    public string valid_from { get; set; }
    public string serial_number { get; set; }
    public string cert { get; set; }
    public int? version { get; set; }
    public bool? is_CA { get; set; }
    public string subject_dn { get; set; }
    public int? path_length_constraint { get; set; }
    public List<string>? subject_alternative_name { get; set; }
    public string valid_to { get; set; }
    public string signature_algorithm { get; set; }
    public List<string> authority_information_access_uri { get; set; }
    public List<string> extended_key_usage { get; set; }
}

public class VCenterTlsCertSet
{
    public string cert { get; set; }
    public string key { get; set; }
    public string root_cert { get; set; }
}

public class VCenterTrustedRootChainsSummary
{
    public string chain { get; set; }
}

public class VCenterTrustedRootChainsInfo
{
    public VCenterX509CertChain? cert_chain { get; set; }
}

public class VCenterX509CertChain
{
    public List<string>? cert_chain { get; set; }
}

public struct VCenterTrustedRootChainsCreate
{
    public VCenterX509CertChain? cert_chain;
    public string chain;
}
#nullable restore
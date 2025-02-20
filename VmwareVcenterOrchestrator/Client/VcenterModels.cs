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
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;

public struct VCenterTlsCertInfo
{
    public string issuer_dn;
    public List<string> key_usage;
    public string thumbprint;
    public string valid_from;
    public string serial_number;
    public string cert;
    public int version;
    public string subject_dn;
    public int path_length_constraint;
    public List<string> subject_alternative_name;
    public string valid_to;
    public string signature_algorithm;
    public List<string> authority_information_access_uri;
    public List<string> extended_key_usage;
}

public struct VCenterTlsCertSet
{
    public string cert;
    public string key;
    public string root_cert;
}

public struct VCenterTrustedRootChainsSummary
{
    public string chain;
}

public struct VCenterTrustedRootChainsInfo
{
    public VCenterX509CertChain cert_chain;
}

public struct VCenterX509CertChain
{
    public List<string> cert_chain;
}

public struct VCenterTrustedRootChainsCreate
{
    public VCenterX509CertChain cert_chain;
    public string chain;
}
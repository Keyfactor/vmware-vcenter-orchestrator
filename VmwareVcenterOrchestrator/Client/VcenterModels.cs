namespace Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;

public struct VcenterCertificateManagementVcenterTlsInfo
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

public struct VcenterCertificateManagementVcenterTlsSet
{
    public string cert;
    public string key;
    public string root_cert;
}

public struct VcenterCertificateManagementVcenterTrustedRootChainsSummary
{
    public string chain;
}

public struct VcenterCertificateManagementVcenterTrustedRootChainsInfo
{
    public VcenterCertificateManagementX509CertChain cert_chain;
}

public struct VcenterCertificateManagementX509CertChain
{
    public List<string> cert_chain;
}
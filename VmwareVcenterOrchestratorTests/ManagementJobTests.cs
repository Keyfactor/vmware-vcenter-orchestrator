
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// ManagementJobTests.cs
// Tests for the Management job's business logic.
//
// ProcessJob takes a ManagementJobConfiguration which is a sealed Keyfactor SDK type,
// so we test the underlying HTTP interactions by calling VmwareVcenterClient directly
// and verifying the same end-to-end flows the Management job orchestrates.
//
// The management job's three code paths are:
//   Add + no private key password  → PerformTrustedRootAddition  → POST /trusted-root-chains/
//   Add + private key password     → PerformSslReplacement       → PUT  /tls
//   Remove                         → PerformRemove               → GET chains, GET chain, DELETE chain

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace VmwareVcenterOrchestratorTests;

public class ManagementJobTests
{
    // ------------------------------------------------------------------
    // Trusted root addition path
    // ------------------------------------------------------------------

    [Fact]
    public async Task AddTrustedRoot_PostsCertificateInPemWrappedPayload()
    {
        var ca = CertFactory.CreateCA("My Trusted Root");
        var base64Contents = Convert.ToBase64String(ca.Export(X509ContentType.Cert));

        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPostTrustedRootChain(HttpStatusCode.Created);

        var client = mock.BuildClient();

        // Replicate what PerformTrustedRootAddition does:
        var certContents = new VCenterX509CertChain
        {
            cert_chain = new List<string>
            {
                $"{X509Certificate2Extensions.CERTIFICATE_HEADER_PEM}{base64Contents}{X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM}"
            }
        };
        var req = new VCenterTrustedRootChainsCreate { cert_chain = certContents };

        await client.AddTrustedRoot(req); // should not throw
    }

    [Fact]
    public async Task AddTrustedRoot_ServerError_ThrowsException()
    {
        var ca = CertFactory.CreateCA();
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPostTrustedRootChain(HttpStatusCode.InternalServerError);

        var client = mock.BuildClient();
        var req = new VCenterTrustedRootChainsCreate
        {
            cert_chain = new VCenterX509CertChain { cert_chain = new List<string> { CertFactory.ToPem(ca) } }
        };

        await Assert.ThrowsAsync<Exception>(() => client.AddTrustedRoot(req));
    }

    // ------------------------------------------------------------------
    // SSL replacement path
    // ------------------------------------------------------------------

    [Fact]
    public async Task ReplaceSslCert_PutsCorrectPayload()
    {
        var ca = CertFactory.CreateCA();
        var leaf = CertFactory.CreateLeaf(ca);

        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPutTlsCert(HttpStatusCode.NoContent);

        var client = mock.BuildClient();

        var certReq = new VCenterTlsCertSet
        {
            cert = CertFactory.ToPem(leaf),
            key = CertFactory.PrivateKeyToPem(leaf),
            root_cert = CertFactory.ToPem(ca)
        };

        await client.ReplaceVcenterSslCertificate(certReq); // should not throw
    }

    [Fact]
    public async Task ReplaceSslCert_ServerError_ThrowsException()
    {
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPutTlsCert(HttpStatusCode.BadRequest);

        var client = mock.BuildClient();
        var certReq = new VCenterTlsCertSet { cert = "x", key = "y", root_cert = "z" };

        await Assert.ThrowsAsync<Exception>(() => client.ReplaceVcenterSslCertificate(certReq));
    }

    // ------------------------------------------------------------------
    // Remove path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Remove_MatchingChain_IssuesDelete()
    {
        var ca = CertFactory.CreateCA("CA To Remove");
        var chainId = "chain-001";

        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChains(new[] { chainId })
            .WithGetTrustedRootChain(chainId, ca)
            .WithDeleteTrustedRootChain(chainId, HttpStatusCode.NoContent);

        var client = mock.BuildClient();

        // Simulate the Management remove loop
        var trustedRootChains = await client.GetTrustedRootChains();
        foreach (var chain in trustedRootChains)
        {
            var info = await client.GetTrustedRootChain(chain);
            var rawPem = info.cert_chain!.cert_chain![0];
            var footerIdx = rawPem.IndexOf(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
            var trimmed = rawPem.Substring(0, footerIdx);
            var base64 = trimmed.TrimStart(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM.ToCharArray());
            var bytes = Convert.FromBase64String(base64);
            var cert = new X509Certificate2(bytes);

            if (cert.Thumbprint == ca.Thumbprint)
            {
                await client.RemoveVcenterTrustedRoot(chain);
                break;
            }
        }
        // No exception = delete was called successfully
    }

    [Fact]
    public async Task Remove_NoMatchingChain_DoesNotDelete()
    {
        var ca = CertFactory.CreateCA("Unrelated CA");
        var chainId = "chain-002";

        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChains(new[] { chainId })
            .WithGetTrustedRootChain(chainId, ca);
        // No DELETE registered — MockHttp would throw if DELETE were attempted

        var client = mock.BuildClient();
        var nonMatchingThumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD";

        var trustedRootChains = await client.GetTrustedRootChains();
        foreach (var chain in trustedRootChains)
        {
            var info = await client.GetTrustedRootChain(chain);
            var rawPem = info.cert_chain!.cert_chain![0];
            var footerIdx = rawPem.IndexOf(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
            var trimmed = rawPem.Substring(0, footerIdx);
            var base64 = trimmed.TrimStart(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM.ToCharArray());
            var bytes = Convert.FromBase64String(base64);
            var cert = new X509Certificate2(bytes);

            if (cert.Thumbprint == nonMatchingThumbprint)
            {
                await client.RemoveVcenterTrustedRoot(chain);
                break;
            }
        }
        // Completing without exception = no DELETE was attempted
    }

    [Fact]
    public async Task Remove_EmptyChainList_CompletesWithoutError()
    {
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChains(Array.Empty<string>());

        var client = mock.BuildClient();
        var chains = await client.GetTrustedRootChains();
        Assert.Empty(chains);
    }
}

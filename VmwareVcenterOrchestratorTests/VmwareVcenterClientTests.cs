
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// VmwareVcenterClientTests.cs
// Unit tests for VmwareVcenterClient — all HTTP calls are intercepted by MockHttp,
// so no live vCenter is needed.

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using System.Net;
using Xunit;

namespace VmwareVcenterOrchestratorTests;

public class VmwareVcenterClientTests
{
    // ------------------------------------------------------------------
    // GetVcenterSslCertificate
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetVcenterSslCertificate_ReturnsDeserializedCert()
    {
        // Arrange
        var leafCert = CertFactory.CreateLeaf(CertFactory.CreateCA());
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTlsCert(leafCert);

        var client = mock.BuildClient();

        // Act
        var result = await client.GetVcenterSslCertificate();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(leafCert.Thumbprint, result.thumbprint);
        Assert.Contains("-----BEGIN CERTIFICATE-----", result.cert);
    }

    [Fact]
    public async Task GetVcenterSslCertificate_ThrowsOnErrorResponse()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTlsCertError(HttpStatusCode.Unauthorized);

        var client = mock.BuildClient();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.GetVcenterSslCertificate());
    }

    // ------------------------------------------------------------------
    // ReplaceVcenterSslCertificate
    // ------------------------------------------------------------------

    [Fact]
    public async Task ReplaceVcenterSslCertificate_SucceedsOnNoContent()
    {
        // Arrange
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

        // Act — should not throw
        await client.ReplaceVcenterSslCertificate(certReq);
    }

    [Fact]
    public async Task ReplaceVcenterSslCertificate_ThrowsOnServerError()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPutTlsCert(HttpStatusCode.InternalServerError);

        var client = mock.BuildClient();
        var certReq = new VCenterTlsCertSet { cert = "x", key = "y", root_cert = "z" };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.ReplaceVcenterSslCertificate(certReq));
    }

    // ------------------------------------------------------------------
    // GetTrustedRootChains
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetTrustedRootChains_ReturnsChainIds()
    {
        // Arrange
        var chainIds = new[] { "chain-aaa", "chain-bbb" };
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChains(chainIds);

        var client = mock.BuildClient();

        // Act
        var result = await client.GetTrustedRootChains();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("chain-aaa", result);
        Assert.Contains("chain-bbb", result);
    }

    [Fact]
    public async Task GetTrustedRootChains_ReturnsEmptyListWhenNoChainsExist()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChains(Array.Empty<string>());

        var client = mock.BuildClient();

        // Act
        var result = await client.GetTrustedRootChains();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTrustedRootChains_ThrowsOnServerError()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChainsError(HttpStatusCode.InternalServerError);

        var client = mock.BuildClient();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.GetTrustedRootChains());
    }

    // ------------------------------------------------------------------
    // GetTrustedRootChain (single chain)
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetTrustedRootChain_ReturnsDeserializedChain()
    {
        // Arrange
        var ca = CertFactory.CreateCA("My Root CA");
        var chainId = "chain-001";
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithGetTrustedRootChain(chainId, ca);

        var client = mock.BuildClient();

        // Act
        var result = await client.GetTrustedRootChain(chainId);

        // Assert
        Assert.NotNull(result.cert_chain);
        Assert.NotEmpty(result.cert_chain!.cert_chain!);
        Assert.Contains("-----BEGIN CERTIFICATE-----", result.cert_chain.cert_chain[0]);
    }

    // ------------------------------------------------------------------
    // RemoveVcenterTrustedRoot
    // ------------------------------------------------------------------

    [Fact]
    public async Task RemoveVcenterTrustedRoot_SucceedsOnNoContent()
    {
        // Arrange
        var chainId = "chain-to-delete";
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithDeleteTrustedRootChain(chainId, HttpStatusCode.NoContent);

        var client = mock.BuildClient();

        // Act — should not throw
        await client.RemoveVcenterTrustedRoot(chainId);
    }

    [Fact]
    public async Task RemoveVcenterTrustedRoot_ThrowsOnNotFound()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithDeleteTrustedRootChain("missing-chain", HttpStatusCode.NotFound);

        var client = mock.BuildClient();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.RemoveVcenterTrustedRoot("missing-chain"));
    }

    // ------------------------------------------------------------------
    // AddTrustedRoot
    // ------------------------------------------------------------------

    [Fact]
    public async Task AddTrustedRoot_SucceedsOnCreated()
    {
        // Arrange
        var ca = CertFactory.CreateCA();
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPostTrustedRootChain(HttpStatusCode.Created);

        var client = mock.BuildClient();

        var payload = new VCenterTrustedRootChainsCreate
        {
            cert_chain = new VCenterX509CertChain
            {
                cert_chain = new List<string> { CertFactory.ToPem(ca) }
            }
        };

        // Act — should not throw
        await client.AddTrustedRoot(payload);
    }

    [Fact]
    public async Task AddTrustedRoot_ThrowsOnServerError()
    {
        // Arrange
        var mock = new VcenterHttpMockBuilder()
            .WithSession()
            .WithPostTrustedRootChain(HttpStatusCode.InternalServerError);

        var client = mock.BuildClient();

        var payload = new VCenterTrustedRootChainsCreate
        {
            cert_chain = new VCenterX509CertChain { cert_chain = new List<string> { "bad-cert" } }
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.AddTrustedRoot(payload));
    }
}

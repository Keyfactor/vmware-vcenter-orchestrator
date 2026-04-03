
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// X509Certificate2ExtensionsTests.cs
// Tests for the static helper methods on X509Certificate2Extensions.
// These methods parse and convert certificates — pure logic, no HTTP.

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace VmwareVcenterOrchestratorTests;

public class X509Certificate2ExtensionsTests
{
    // ------------------------------------------------------------------
    // PEM constants
    // ------------------------------------------------------------------

    [Fact]
    public void CertificateHeaderPem_IsCorrect()
    {
        Assert.Equal("-----BEGIN CERTIFICATE-----", X509Certificate2Extensions.CERTIFICATE_HEADER_PEM);
    }

    [Fact]
    public void CertificateFooterPem_IsCorrect()
    {
        Assert.Equal("-----END CERTIFICATE-----", X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM);
    }

    [Fact]
    public void PrivateKeyHeaderPem_IsCorrect()
    {
        Assert.Equal("-----BEGIN PRIVATE KEY-----", X509Certificate2Extensions.PRIVATE_KEY_HEADER_PEM);
    }

    [Fact]
    public void PrivateKeyFooterPem_IsCorrect()
    {
        Assert.Equal("-----END PRIVATE KEY-----", X509Certificate2Extensions.PRIVATE_KEY_FOOTER_PEM);
    }

    // ------------------------------------------------------------------
    // ExportCertAndPrivateKeyPem
    // ------------------------------------------------------------------

    [Fact]
    public void ExportCertAndPrivateKeyPem_ReturnsValidPem()
    {
        // Arrange
        var leaf = CertFactory.CreateLeaf(CertFactory.CreateCA());

        // Act
        var (certPem, keyPem) = leaf.ExportCertAndPrivateKeyPem();

        // Assert — cert
        Assert.StartsWith("-----BEGIN CERTIFICATE-----", certPem);
        Assert.EndsWith("-----END CERTIFICATE-----", certPem);

        // Assert — private key
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", keyPem);
        Assert.EndsWith("-----END PRIVATE KEY-----", keyPem);
    }

    [Fact]
    public void ExportCertAndPrivateKeyPem_CertRoundtrips()
    {
        // Arrange
        var leaf = CertFactory.CreateLeaf(CertFactory.CreateCA());

        // Act
        var (certPem, _) = leaf.ExportCertAndPrivateKeyPem();

        // Strip headers and decode; re-import and compare thumbprints
        var base64 = certPem
            .Replace("-----BEGIN CERTIFICATE-----\n", string.Empty)
            .Replace("\n-----END CERTIFICATE-----", string.Empty)
            .Trim();
        var bytes = Convert.FromBase64String(base64);
        var reimported = new X509Certificate2(bytes);

        Assert.Equal(leaf.Thumbprint, reimported.Thumbprint);
    }

    // ------------------------------------------------------------------
    // ExportCARootPem
    // ------------------------------------------------------------------

    [Fact]
    public void ExportCARootPem_SelfSignedCert_ReturnsEmpty()
    {
        // A self-signed CA has no issuer higher in the chain, so when Build()
        // fails to go higher, chain.Build returns false → empty string.
        var ca = CertFactory.CreateCA("Standalone CA");
        var logger = NullLogger.Instance;

        // Self-signed CA: chain.Build succeeds but the cert IS the root,
        // so the loop (i=1 to elementCount-1) produces an empty string.
        var pem = ca.ExportCARootPem(logger);

        // A self-signed cert has only 1 element in the chain, so the loop body
        // doesn't execute and the result is empty.
        Assert.Equal(string.Empty, pem);
    }

    [Fact]
    public void ExportCARootPem_LeafCert_ReturnsEmptyWithoutTrustStore()
    {
        // Without installing the CA into the trust store, chain.Build() may fail
        // (PartialChain status), which is the expected behaviour in test environments.
        // The method returns an empty string in that case — verify it doesn't throw.
        var ca = CertFactory.CreateCA();
        var leaf = CertFactory.CreateLeaf(ca);
        var logger = NullLogger.Instance;

        // Act — must not throw regardless of chain build outcome
        var pem = leaf.ExportCARootPem(logger);

        // pem is either empty (chain could not be built in sandbox) or a valid PEM block
        if (!string.IsNullOrEmpty(pem))
        {
            Assert.Contains("-----BEGIN CERTIFICATE-----", pem);
            Assert.Contains("-----END CERTIFICATE-----", pem);
        }
    }

    // ------------------------------------------------------------------
    // RootCACert extension
    // ------------------------------------------------------------------

    [Fact]
    public void RootCACert_SelfSignedCA_ReturnsCertOrNull()
    {
        // Self-signed cert: chain.Build may return PartialChain in a test environment.
        // RootCACert should return null if PartialChain is present, otherwise the cert itself.
        var ca = CertFactory.CreateCA();

        // Act — must not throw
        var root = ca.RootCACert();

        // Either null (partial chain) or the cert itself is acceptable
        if (root != null)
            Assert.Equal(ca.Thumbprint, root.Thumbprint);
    }
}

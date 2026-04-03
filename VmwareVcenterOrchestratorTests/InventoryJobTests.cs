
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// InventoryJobTests.cs
// Tests for the Inventory.FormatSslCert and Inventory.FormatTrustedRoot helper methods.
// These are the pure-logic methods that transform vCenter API responses into
// Keyfactor inventory items — no HTTP or orchestrator framework needed.

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs;
using Keyfactor.Logging;
using Xunit;

namespace VmwareVcenterOrchestratorTests;

public class InventoryJobTests
{
    // The Inventory class requires an IPAMSecretResolver via its constructor.
    // Passing null is safe because the resolver is only used inside Initialize(),
    // which we don't call in these formatting-only tests.
    //
    // _logger is declared as  internal protected ILogger _logger { get; set; }
    // on the VmwareVcenterJob base class, so it has a real setter we can call
    // directly without reflection. It must be set before any Format* call because
    // those methods call _logger.MethodEntry() and similar.
    private static Inventory CreateInventory()
    {
        var inventory = new Inventory(null!);
        inventory._logger = LogHandler.GetClassLogger<Inventory>();
        return inventory;
    }

    // ------------------------------------------------------------------
    // FormatSslCert
    // ------------------------------------------------------------------

    [Fact]
    public void FormatSslCert_ReturnsSingleInventoryItem()
    {
        // Arrange
        var inventory = CreateInventory();
        var cert = CertFactory.CreateLeaf(CertFactory.CreateCA());

        var tlsInfo = new VCenterTlsCertInfo
        {
            thumbprint = cert.Thumbprint,
            cert = CertFactory.ToPem(cert),
            issuer_dn = cert.Issuer,
            subject_dn = cert.Subject,
            valid_from = cert.NotBefore.ToString("o"),
            valid_to = cert.NotAfter.ToString("o"),
            serial_number = cert.SerialNumber,
            signature_algorithm = "sha256WithRSAEncryption",
            key_usage = new List<string> { "digitalSignature" },
            authority_information_access_uri = new List<string>(),
            extended_key_usage = new List<string> { "serverAuth" },
            subject_alternative_name = new List<string> { "vcenter.example.com" }
        };

        // Act
        var result = inventory.FormatSslCert(tlsInfo).ToList();

        // Assert
        Assert.Single(result);
        var item = result[0];
        Assert.Equal(cert.Thumbprint, item.Alias);
        Assert.True(item.PrivateKeyEntry);
        Assert.True(item.UseChainLevel);
        Assert.Single(item.Certificates);
        // The PEM headers should have been stripped
        Assert.DoesNotContain("-----BEGIN CERTIFICATE-----", item.Certificates.First());
        Assert.DoesNotContain("-----END CERTIFICATE-----", item.Certificates.First());
    }

    [Fact]
    public void FormatSslCert_StripsPemHeaders()
    {
        // Arrange
        var inventory = CreateInventory();
        var cert = CertFactory.CreateLeaf(CertFactory.CreateCA());
        var pem = CertFactory.ToPem(cert); // Has BEGIN/END headers

        var tlsInfo = new VCenterTlsCertInfo
        {
            thumbprint = cert.Thumbprint,
            cert = pem,
            issuer_dn = cert.Issuer,
            subject_dn = cert.Subject,
            valid_from = cert.NotBefore.ToString("o"),
            valid_to = cert.NotAfter.ToString("o"),
            serial_number = cert.SerialNumber,
            signature_algorithm = "sha256WithRSAEncryption",
            key_usage = new List<string>(),
            authority_information_access_uri = new List<string>(),
            extended_key_usage = new List<string>()
        };

        // Act
        var result = inventory.FormatSslCert(tlsInfo).ToList();

        // Assert: raw base64, no PEM wrappers
        var certContent = result[0].Certificates.First();
        Assert.DoesNotContain(X509Certificate2Extensions.CERTIFICATE_HEADER_PEM, certContent);
        Assert.DoesNotContain(X509Certificate2Extensions.CERTIFICATE_FOOTER_PEM, certContent);
        // Verify it's still valid base64
        var bytes = Convert.FromBase64String(certContent.Trim());
        Assert.NotEmpty(bytes);
    }

    // ------------------------------------------------------------------
    // FormatTrustedRoot
    // ------------------------------------------------------------------

    [Fact]
    public void FormatTrustedRoot_ValidCert_ReturnsInventoryItem()
    {
        // Arrange
        var inventory = CreateInventory();
        var ca = CertFactory.CreateCA("My Root CA");

        var trustedRootInfo = new VCenterTrustedRootChainsInfo
        {
            cert_chain = new VCenterX509CertChain
            {
                cert_chain = new List<string> { CertFactory.ToPem(ca) }
            }
        };

        // Act
        var item = inventory.FormatTrustedRoot(trustedRootInfo);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
        Assert.False(item.PrivateKeyEntry);
        Assert.True(item.UseChainLevel);
        Assert.Single(item.Certificates);
    }

    [Fact]
    public void FormatTrustedRoot_NullInput_ReturnsNull()
    {
        // Arrange
        var inventory = CreateInventory();

        // Act
        var item = inventory.FormatTrustedRoot(null!);

        // Assert
        Assert.Null(item);
    }

    [Fact]
    public void FormatTrustedRoot_EmptyCertChain_ReturnsNull()
    {
        // Arrange
        var inventory = CreateInventory();
        var trustedRootInfo = new VCenterTrustedRootChainsInfo
        {
            cert_chain = new VCenterX509CertChain
            {
                cert_chain = new List<string>()
            }
        };

        // Act
        var item = inventory.FormatTrustedRoot(trustedRootInfo);

        // Assert
        Assert.Null(item);
    }

    [Fact]
    public void FormatTrustedRoot_NoCertChainObject_ReturnsNull()
    {
        // Arrange
        var inventory = CreateInventory();
        var trustedRootInfo = new VCenterTrustedRootChainsInfo
        {
            cert_chain = null
        };

        // Act — should not throw, null cert_chain means no data
        // Note: the production code accesses cert_chain?.cert_chain?.Any() so this
        // exercises that null-safe path
        var item = inventory.FormatTrustedRoot(trustedRootInfo);

        // Assert
        Assert.Null(item);
    }

    [Fact]
    public void FormatTrustedRoot_MissingPemHeader_ReturnsNull()
    {
        // Arrange: strip the PEM header, as if the data is malformed
        var inventory = CreateInventory();
        var ca = CertFactory.CreateCA();
        var pem = CertFactory.ToPem(ca);
        var stripped = pem.Replace("-----BEGIN CERTIFICATE-----\n", string.Empty);

        var trustedRootInfo = new VCenterTrustedRootChainsInfo
        {
            cert_chain = new VCenterX509CertChain
            {
                cert_chain = new List<string> { stripped }
            }
        };

        // Act
        var item = inventory.FormatTrustedRoot(trustedRootInfo);

        // Assert: no PEM header → production code returns null
        Assert.Null(item);
    }
}

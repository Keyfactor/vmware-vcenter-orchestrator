
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// TestFixtures.cs
// Shared helpers: certificate factories, JSON response builders, and fake HTTP handler wiring.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RichardSzalay.MockHttp;

namespace VmwareVcenterOrchestratorTests;

// ---------------------------------------------------------------------------
// Certificate factory helpers
// ---------------------------------------------------------------------------

public static class CertFactory
{
    /// <summary>Creates a self-signed CA certificate, always exportable.</summary>
    public static X509Certificate2 CreateCA(string subjectName = "Test CA")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, false));

        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        // Round-trip through PFX with Exportable so the CNG key allows ExportPkcs8PrivateKey()
        return new X509Certificate2(cert.Export(X509ContentType.Pfx, "pw"), "pw", X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Creates a leaf certificate signed by <paramref name="ca"/>, always exportable.
    /// The private key is round-tripped through PFX export/import so that the resulting
    /// CNG key is marked Exportable — required on Windows for ExportPkcs8PrivateKey().
    /// Without this, CopyWithPrivateKey() produces a CNG-backed cert whose key blocks export
    /// even though the RSA object itself was created in memory.
    /// </summary>
    public static X509Certificate2 CreateLeaf(X509Certificate2 ca, string subjectName = "vcenter.example.com")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(subjectName);
        req.CertificateExtensions.Add(san.Build());

        var signed = req.Create(ca, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1),
            req.PublicKey.EncodedKeyValue.RawData);

        // CopyWithPrivateKey alone gives a non-exportable CNG key on Windows.
        // Exporting to PFX and re-importing with Exportable fixes this.
        var withKey = signed.CopyWithPrivateKey(rsa);
        return new X509Certificate2(withKey.Export(X509ContentType.Pfx, "pw"), "pw",
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }

    /// <summary>
    /// Exports a certificate as a PEM string whose base64 body is split into
    /// 64-character lines, matching the RFC 7468 / OpenSSL format that the
    /// production FormatTrustedRoot parsing code expects.
    ///
    /// FormatTrustedRoot does index arithmetic relative to the header/footer positions.
    /// The header constant is "-----BEGIN CERTIFICATE-----" (no trailing newline),
    /// so certStartIndex = headerIndex + header.Length - 1 lands on the last '-' of
    /// the header. The production code then calls Substring(certStartIndex, ...) and
    /// Trim('\n','\r') to strip that stray character — which only works cleanly when
    /// the body is on a separate line immediately after the header.
    /// Putting the entire base64 on one line (no wrapping, no newline after header)
    /// causes the Substring to include the trailing '-' as part of the base64 body,
    /// which makes Convert.FromBase64String throw a FormatException.
    /// </summary>
    public static string ToPem(X509Certificate2 cert)
    {
        var raw = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
        // Wrap at 64 chars per line — matches what OpenSSL / vCenter actually returns
        var wrapped = WrapBase64(raw, 64);
        return $"-----BEGIN CERTIFICATE-----\n{wrapped}\n-----END CERTIFICATE-----";
    }

    /// <summary>Exports an RSA private key as a PKCS#8 PEM string.</summary>
    public static string PrivateKeyToPem(X509Certificate2 cert)
    {
        var rsa = cert.GetRSAPrivateKey() ?? throw new InvalidOperationException(
            "No RSA private key found.");

        // On Windows GetRSAPrivateKey() returns an RSACng whose CNG key only has
        // AllowExport (encrypted export) set, not AllowPlaintextExport (needed for
        // ExportPkcs8PrivateKey). Set AllowPlaintextExport explicitly before exporting.
        if (rsa is System.Security.Cryptography.RSACng rsaCng)
        {
            rsaCng.Key.SetProperty(
                new System.Security.Cryptography.CngProperty(
                    "Export Policy",
                    BitConverter.GetBytes((int)System.Security.Cryptography.CngExportPolicies.AllowPlaintextExport),
                    System.Security.Cryptography.CngPropertyOptions.None));
        }

        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        return $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(pkcs8)}\n-----END PRIVATE KEY-----";
    }

    // Splits a flat base64 string into lines of <lineLength> characters
    private static string WrapBase64(string base64, int lineLength)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < base64.Length; i += lineLength)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(base64, i, Math.Min(lineLength, base64.Length - i));
        }
        return sb.ToString();
    }
}

// ---------------------------------------------------------------------------
// vCenter REST API JSON response helpers
// ---------------------------------------------------------------------------

public static class VcenterResponses
{
    public static string SessionToken(string token = "fake-session-token") =>
        JsonSerializer.Serialize(token); // the real API returns a quoted JSON string

    public static string TlsCertInfo(X509Certificate2 cert) => JsonSerializer.Serialize(new
    {
        issuer_dn = cert.Issuer,
        key_usage = new[] { "digitalSignature" },
        thumbprint = cert.Thumbprint,
        valid_from = cert.NotBefore.ToString("o"),
        serial_number = cert.SerialNumber,
        cert = CertFactory.ToPem(cert),
        version = 3,
        is_CA = false,
        subject_dn = cert.Subject,
        path_length_constraint = (int?)null,
        subject_alternative_name = new[] { "vcenter.example.com" },
        valid_to = cert.NotAfter.ToString("o"),
        signature_algorithm = "sha256WithRSAEncryption",
        authority_information_access_uri = Array.Empty<string>(),
        extended_key_usage = new[] { "serverAuth" }
    });

    public static string TrustedRootChainList(IEnumerable<string> chains) =>
        JsonSerializer.Serialize(chains.Select(c => new { chain = c }));

    public static string TrustedRootChainInfo(X509Certificate2 cert) => JsonSerializer.Serialize(new
    {
        cert_chain = new
        {
            cert_chain = new[] { CertFactory.ToPem(cert) }
        }
    });

    // Simulates a vCenter error response body
    public static string ErrorBody(string message) =>
        JsonSerializer.Serialize(new { error_type = "ERROR", messages = new[] { new { default_message = message } } });
}

// ---------------------------------------------------------------------------
// MockHttpMessageHandler factory
// ---------------------------------------------------------------------------

public class VcenterHttpMockBuilder
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly string _baseUrl;

    public VcenterHttpMockBuilder(string baseUrl = "https://vcenter.example.com")
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public VcenterHttpMockBuilder WithSession(string token = "fake-session-token")
    {
        _handler.When(HttpMethod.Post, $"{_baseUrl}/api/session")
                .Respond(HttpStatusCode.OK, "application/json", VcenterResponses.SessionToken(token));
        return this;
    }

    public VcenterHttpMockBuilder WithGetTlsCert(X509Certificate2 cert)
    {
        _handler.When(HttpMethod.Get, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/tls")
                .Respond(HttpStatusCode.OK, "application/json", VcenterResponses.TlsCertInfo(cert));
        return this;
    }

    public VcenterHttpMockBuilder WithGetTlsCertError(HttpStatusCode status, string body = "Unauthorized")
    {
        _handler.When(HttpMethod.Get, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/tls")
                .Respond(status, "application/json", body);
        return this;
    }

    public VcenterHttpMockBuilder WithPutTlsCert(HttpStatusCode status = HttpStatusCode.NoContent)
    {
        _handler.When(HttpMethod.Put, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/tls")
                .Respond(status);
        return this;
    }

    public VcenterHttpMockBuilder WithGetTrustedRootChains(IEnumerable<string> chains)
    {
        _handler.When(HttpMethod.Get, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/trusted-root-chains/")
                .Respond(HttpStatusCode.OK, "application/json", VcenterResponses.TrustedRootChainList(chains));
        return this;
    }

    public VcenterHttpMockBuilder WithGetTrustedRootChain(string chainId, X509Certificate2 cert)
    {
        _handler.When(HttpMethod.Get, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/trusted-root-chains/{chainId}")
                .Respond(HttpStatusCode.OK, "application/json", VcenterResponses.TrustedRootChainInfo(cert));
        return this;
    }

    public VcenterHttpMockBuilder WithDeleteTrustedRootChain(string chainId, HttpStatusCode status = HttpStatusCode.NoContent)
    {
        _handler.When(HttpMethod.Delete, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/trusted-root-chains/{chainId}")
                .Respond(status);
        return this;
    }

    public VcenterHttpMockBuilder WithPostTrustedRootChain(HttpStatusCode status = HttpStatusCode.Created)
    {
        _handler.When(HttpMethod.Post, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/trusted-root-chains/")
                .Respond(status);
        return this;
    }

    public VcenterHttpMockBuilder WithGetTrustedRootChainsError(HttpStatusCode status, string body = "Server Error")
    {
        _handler.When(HttpMethod.Get, $"{_baseUrl}/api/vcenter/certificate-management/vcenter/trusted-root-chains/")
                .Respond(status, "application/json", body);
        return this;
    }

    public MockHttpMessageHandler Handler => _handler;

    public Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client.VmwareVcenterClient BuildClient(
        string hostname = "vcenter.example.com",
        string username = "admin@vsphere.local",
        string password = "password")
    {
        return TestableVmwareVcenterClient.Create(_handler, hostname, username, password);
    }
}

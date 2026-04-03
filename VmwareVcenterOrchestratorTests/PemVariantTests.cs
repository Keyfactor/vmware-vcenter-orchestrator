
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// PemVariantTests.cs
// Tests for FormatTrustedRoot against the range of PEM formats that vCenter
// may return in the trusted-root-chains API response.
//
// WHAT VCENTER ACTUALLY RETURNS
// ==============================
// The vSphere Automation API docs describe the cert_chain field as
// "Certificate chain in base64 encoding" — no line-length constraint is
// specified at the API level. Underneath, vCenter stores certs in VECS
// (VMware Endpoint Certificate Store) and serialises them via OpenSSL/NSS,
// which is permissive on input.  Real-world observations and KB articles show:
//
//   1. Standard RFC 7468 / OpenSSL format: 64-char wrapped lines
//   2. Single-line base64 (entire body on one line, no wrapping)
//   3. Literal \n in JSON (newlines encoded as the two characters \n rather
//      than actual 0x0A bytes — happens when the PEM was pasted into a JSON
//      payload via curl/scripts and not re-encoded)
//   4. Windows CRLF line endings (\r\n instead of \n)
//   5. Bag Attributes block prepended by OpenSSL before the BEGIN header
//   6. Trailing whitespace / blank lines after the END footer
//   7. Non-standard line lengths (e.g. 76-char MIME, 32-char, or 1-char)
//
// WHAT THE PRODUCTION PARSER DOES
// =================================
// FormatTrustedRoot does index-based extraction:
//
//   headerStartIndex = cert.IndexOf("-----BEGIN CERTIFICATE-----")
//   footerStartIndex = cert.IndexOf("-----END CERTIFICATE-----")
//   certStartIndex   = headerStartIndex + HEADER.Length - 1   // ← the -1 is intentional:
//                                                              //   it lands on the \n after the header
//   certContent      = cert.Substring(certStartIndex, footerStartIndex - 1 - certStartIndex)
//   certContent      = certContent.Trim('\n', '\r').Trim()
//   bytes            = Convert.FromBase64String(certContent)
//
// The -1 trick works when:
//   • There is exactly one \n immediately after the header.
//   • The body contains ONLY valid base64 characters and whitespace.
//
// It BREAKS when:
//   • The header is immediately followed by the base64 body (no \n) —
//     the -1 lands on the last '-' of the header, corrupting the base64.
//   • The body contains non-base64 text (e.g. literal \n characters, or
//     embedded CRL data after the END footer that wasn't trimmed first).
//
// These tests document both the currently-working and currently-broken cases,
// giving you a clear picture of where the parser needs hardening.

using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Jobs;
using Keyfactor.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace VmwareVcenterOrchestratorTests;

public class PemVariantTests
{
    private static Inventory CreateInventory()
    {
        var inv = new Inventory(null!);
        inv._logger = LogHandler.GetClassLogger<Inventory>();
        return inv;
    }

    // Builds a VCenterTrustedRootChainsInfo from a raw PEM string
    private static VCenterTrustedRootChainsInfo MakeInfo(string pemString) =>
        new VCenterTrustedRootChainsInfo
        {
            cert_chain = new VCenterX509CertChain
            {
                cert_chain = new List<string> { pemString }
            }
        };

    // Extracts just the raw base64 body from a PEM (no headers, no whitespace)
    private static string RawBase64(X509Certificate2 cert) =>
        Convert.ToBase64String(cert.Export(X509ContentType.Cert));

    // -----------------------------------------------------------------------
    // Variant 1 — Standard 64-char wrapped (RFC 7468 / OpenSSL default)
    // This is the canonical format and MUST work.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_Standard64CharWrapped_Succeeds()
    {
        var ca = CertFactory.CreateCA("Standard CA");
        var pem = CertFactory.ToPem(ca); // already 64-char wrapped
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 2 — Single-line base64 (no line wrapping)
    // vCenter has been observed returning this for certs that were originally
    // pasted into the API without reformatting.
    // The production parser lands on the last '-' of the header when there
    // is no \n after it, causing FromBase64String to throw.
    // This test documents the current behaviour; if it passes after a parser
    // fix it should continue to pass.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_SingleLineBody_Succeeds()
    {
        var ca = CertFactory.CreateCA("SingleLine CA");
        var pem = $"-----BEGIN CERTIFICATE-----\n{RawBase64(ca)}\n-----END CERTIFICATE-----";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 3 — CRLF line endings (\r\n)
    // Happens when the cert was generated or stored on Windows, or when the
    // JSON response was produced by a Windows-hosted vCenter.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_CrlfLineEndings_Succeeds()
    {
        var ca = CertFactory.CreateCA("CRLF CA");
        var body = WrapBase64(RawBase64(ca), 64).Replace("\n", "\r\n");
        var pem = $"-----BEGIN CERTIFICATE-----\r\n{body}\r\n-----END CERTIFICATE-----";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 4 — Bag Attributes block before the BEGIN header
    // OpenSSL adds this when exporting with -info or when copying from
    // a PKCS#12. vCenter may echo it back if the cert was pasted in with
    // bag attributes still attached.
    // The production code finds BEGIN CERTIFICATE by index, so bag attributes
    // before the header should be ignored.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_BagAttributesBeforeHeader_Succeeds()
    {
        var ca = CertFactory.CreateCA("BagAttr CA");
        var bagAttrs =
            "Bag Attributes\r\n" +
            "    localKeyID: 01 00 00 00\r\n" +
            "subject=/CN=BagAttr CA\r\n" +
            "issuer=/CN=BagAttr CA\r\n";
        var pem = bagAttrs + CertFactory.ToPem(ca);
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 5 — Trailing whitespace / blank lines after END footer
    // Happens when cert files are copy-pasted into JSON fields and the editor
    // appended a trailing newline or spaces.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_TrailingWhitespaceAfterFooter_Succeeds()
    {
        var ca = CertFactory.CreateCA("Trailing WS CA");
        var pem = CertFactory.ToPem(ca) + "\n\n   \n";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 6 — 76-char line wrap (MIME / Java default)
    // Java's Base64.getMimeEncoder() and some older tools wrap at 76 chars.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_76CharWrapped_Succeeds()
    {
        var ca = CertFactory.CreateCA("MIME CA");
        var body = WrapBase64(RawBase64(ca), 76);
        var pem = $"-----BEGIN CERTIFICATE-----\n{body}\n-----END CERTIFICATE-----";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 7 — Literal \n in the JSON string (escaped, not real newlines)
    // When people use curl/bash to POST certs they sometimes do:
    //   sed -E ':a;N;$!ba;s/\r{0,1}\n/\\n/g'
    // so the JSON contains the two characters \ and n instead of 0x0A.
    // If the JSON deserialiser passes those through literally, the base64
    // body contains backslash-n, which is not valid base64.
    // This tests what the parser does with that input — it will likely throw
    // FormatException, which is the correct behaviour since the data is
    // malformed from our parser's perspective.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_LiteralBackslashN_ThrowsOrReturnsNull()
    {
        var ca = CertFactory.CreateCA("LiteralN CA");
        // Build a PEM where actual newlines are replaced with literal \n (2 chars)
        var realPem = CertFactory.ToPem(ca);
        var literalPem = realPem.Replace("\n", "\\n");

        var inventory = CreateInventory();
        // Either throws (FormatException on base64 decode) or returns null —
        // both are acceptable failure modes; it must NOT silently return a
        // wrong certificate.
        Exception? caughtEx = null;
        VCenterTrustedRootChainsInfo? info = null;
        try
        {
            info = MakeInfo(literalPem);
            var item = inventory.FormatTrustedRoot(info);
            // If it didn't throw, it should have returned null (header not found
            // because \n escaping corrupts the header delimiter search)
            Assert.Null(item);
        }
        catch (FormatException ex)
        {
            caughtEx = ex;
        }
        // At least one of the above paths must have been taken
        Assert.True(caughtEx != null || info != null,
            "Expected either a FormatException or a null return for literal-backslash-n input");
    }

    // -----------------------------------------------------------------------
    // Variant 8 — Extra text between END CERTIFICATE and another BEGIN
    // (e.g. a CRL Distribution Point block vCenter occasionally appends)
    // The production code in Management.PerformRemove explicitly strips
    // content after the END footer using IndexOf + Substring.
    // FormatTrustedRoot currently does NOT do this strip, so extra content
    // after the footer is harmless to the Substring extraction (the footer
    // index is used as the end boundary) — but verify it.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_ExtraTextAfterFooter_Succeeds()
    {
        var ca = CertFactory.CreateCA("ExtraText CA");
        var pem = CertFactory.ToPem(ca) +
                  "\n-----BEGIN X509 CRL-----\nMIIBpDCBjQIBATANBgkqhkiG9w0B\n-----END X509 CRL-----\n";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 9 — No newline between header and body (header immediately
    // followed by base64, no separator at all)
    // This is the edge case that breaks the -1 trick in the production parser.
    // Documents the currently-broken behaviour so a fix can be validated.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_NoNewlineAfterHeader_Succeeds()
    {
        var ca = CertFactory.CreateCA("NoNewline CA");
        // Deliberately omit the \n between header and body
        var pem = $"-----BEGIN CERTIFICATE-----{RawBase64(ca)}\n-----END CERTIFICATE-----";
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Variant 10 — Leading whitespace / BOM before the header
    // Some tools prepend a UTF-8 BOM (\xEF\xBB\xBF) or spaces before the
    // header, which IndexOf would skip past correctly.
    // -----------------------------------------------------------------------
    [Fact]
    public void FormatTrustedRoot_LeadingWhitespaceBeforeHeader_Succeeds()
    {
        var ca = CertFactory.CreateCA("LeadWS CA");
        var pem = "   \n" + CertFactory.ToPem(ca);
        var item = CreateInventory().FormatTrustedRoot(MakeInfo(pem));
        Assert.NotNull(item);
        Assert.Equal(ca.Thumbprint, item.Alias);
    }

    // -----------------------------------------------------------------------
    // Helper: same as CertFactory.WrapBase64 but accessible here
    // -----------------------------------------------------------------------
    private static string WrapBase64(string base64, int lineLength)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < base64.Length; i += lineLength)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(base64, i, Math.Min(lineLength, base64.Length - i));
        }
        return sb.ToString();
    }
}

using System.Globalization;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's OpenSSL-build <c>-v</c> lines for a finished handshake, in the order it
/// prints them: the ALPN offer, <c>SSL connection using</c> (<c>Curl_ossl_report_handshake</c>),
/// the ALPN answer, the server certificate and chain, and the verify result
/// (<c>Curl_ossl_check_peer_cert</c>), all in <c>lib/vtls/openssl.c</c> (ADR-0085).
/// </summary>
internal static class OpenSslHandshakeText
{
    // X509_V_ERR_UNSPECIFIED, for a failed verification the platform gave no code for.
    private const long UnspecifiedVerifyError = 1;

    // SSL_get_version's names. SSL 3.0, TLS 1.0 and TLS 1.1 are named by value because
    // their SslProtocols members are obsolete.
    private static readonly Dictionary<SslProtocols, string> VersionNames = new()
    {
        [(SslProtocols)0x30] = "SSLv3",
        [(SslProtocols)0xC0] = "TLSv1",
        [(SslProtocols)0x300] = "TLSv1.1",
        [SslProtocols.Tls12] = "TLSv1.2",
        [SslProtocols.Tls13] = "TLSv1.3",
    };

    // OpenSSL's names for the TLS 1.2 suites it enables by default, where they differ from
    // the IANA names (`openssl ciphers -stdname DEFAULT`, OpenSSL 3.5.7, less PSK and SRP).
    // TLS 1.3 suites and any other keep their IANA name.
    private static readonly Dictionary<TlsCipherSuite, string> CipherNames = new()
    {
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384] = "ECDHE-ECDSA-AES256-GCM-SHA384",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384] = "ECDHE-RSA-AES256-GCM-SHA384",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384] = "DHE-RSA-AES256-GCM-SHA384",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256] = "ECDHE-ECDSA-CHACHA20-POLY1305",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256] = "ECDHE-RSA-CHACHA20-POLY1305",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256] = "DHE-RSA-CHACHA20-POLY1305",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256] = "ECDHE-ECDSA-AES128-GCM-SHA256",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256] = "ECDHE-RSA-AES128-GCM-SHA256",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256] = "DHE-RSA-AES128-GCM-SHA256",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384] = "ECDHE-ECDSA-AES256-SHA384",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384] = "ECDHE-RSA-AES256-SHA384",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256] = "DHE-RSA-AES256-SHA256",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256] = "ECDHE-ECDSA-AES128-SHA256",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256] = "ECDHE-RSA-AES128-SHA256",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256] = "DHE-RSA-AES128-SHA256",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA] = "ECDHE-ECDSA-AES256-SHA",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA] = "ECDHE-RSA-AES256-SHA",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA] = "DHE-RSA-AES256-SHA",
        [TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA] = "ECDHE-ECDSA-AES128-SHA",
        [TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA] = "ECDHE-RSA-AES128-SHA",
        [TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA] = "DHE-RSA-AES128-SHA",
        [TlsCipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384] = "AES256-GCM-SHA384",
        [TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256] = "AES128-GCM-SHA256",
        [TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256] = "AES256-SHA256",
        [TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256] = "AES128-SHA256",
        [TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA] = "AES256-SHA",
        [TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA] = "AES128-SHA",
    };

    /// <summary>Returns the lines for a handshake.</summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    /// <param name="alpnLines">The two ALPN lines, or none when nothing was offered.</param>
    /// <returns>The lines, without the <c>* </c> prefix.</returns>
    internal static IReadOnlyList<string> Lines(TlsHandshakeEvent handshake, IReadOnlyList<string> alpnLines)
    {
        return Lines(handshake, alpnLines, isLibreSsl: false);
    }

    /// <summary>
    /// Returns the lines curl.se's LibreSSL build (curl 8.18.0, LibreSSL 4.2.1) prints for a
    /// handshake, as it does for a QUIC connect on Windows: the OpenSSL lines, but with
    /// <c>[blank] / UNDEF</c> for the group and signature, <c>Public key type ?</c> with no
    /// type or group name, and no <c>OpenSSL verify result</c> line, since LibreSSL lacks the
    /// OpenSSL 3 calls curl takes them from (measured, BL-1050).
    /// </summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    /// <param name="alpnLines">The two ALPN lines, or none when nothing was offered.</param>
    /// <returns>The lines, without the <c>* </c> prefix.</returns>
    internal static IReadOnlyList<string> LibreSslLines(TlsHandshakeEvent handshake, IReadOnlyList<string> alpnLines)
    {
        return Lines(handshake, alpnLines, isLibreSsl: true);
    }

    private static List<string> Lines(TlsHandshakeEvent handshake, IReadOnlyList<string> alpnLines, bool isLibreSsl)
    {
        // A handshake that failed before it negotiated anything printed only the ALPN offer
        // before its ClientHello (exit 35, measured with curl 8.18.0, BL-1178).
        if (handshake.Failed && handshake.ProtocolVersion == SslProtocols.None)
        {
            return [.. alpnLines.Take(1)];
        }

        var lines = new List<string>();
        lines.AddRange(alpnLines.Take(1));
        lines.Add(ConnectionLine(handshake, isLibreSsl));
        if (handshake.EchResult is { } echResult)
        {
            lines.Add("ECH: result: " + echResult);
        }

        lines.AddRange(handshake.EchRetryConfigLines);

        lines.AddRange(alpnLines.Skip(1));
        AddCertificateLines(handshake, isLibreSsl, lines);
        return lines;
    }

    // The server certificate and chain, then the host name check and the verify result.
    private static void AddCertificateLines(TlsHandshakeEvent handshake, bool isLibreSsl, List<string> lines)
    {
        if (handshake.ServerCertificate is { } certificate)
        {
            lines.AddRange(OpenSslCertificateText.PeerCertificate(certificate, handshake.IsProxy));
            lines.AddRange(handshake.PeerCertificateChain
                .Select((chainCertificate, level) => OpenSslCertificateText.CertificateLevel(level, chainCertificate, isLibreSsl))
                .OfType<string>());
            if (HostNameMatches(handshake, certificate, lines) && !CertificateRefused(handshake))
            {
                lines.AddRange(VerifyResult(handshake, isLibreSsl));
                lines.AddRange(TransferEventInfoText.PinnedPublicKeyHashLines(handshake));
            }
        }
    }

    private static string ConnectionLine(TlsHandshakeEvent handshake, bool isLibreSsl)
    {
        var groupName = isLibreSsl ? null : handshake.NegotiatedGroupName;
        var signatureTypeName = isLibreSsl ? null : handshake.PeerSignatureTypeName;
        return $"SSL connection using {VersionNames.GetValueOrDefault(handshake.ProtocolVersion, "unknown")} / " +
            $"{CipherName(handshake.CipherSuite)} / {groupName ?? "[blank]"} / {signatureTypeName ?? "UNDEF"}";
    }

    // ossl_verifyhost runs only when the host name is checked, and curl stops at a mismatch
    // before the verify result.
    private static bool HostNameMatches(TlsHandshakeEvent handshake, X509Certificate2 certificate, List<string> lines)
    {
        if (handshake.VerifiedHostName is not { } hostName)
        {
            return true;
        }

        var matched = OpenSslHostNameText.Matches(certificate, hostName, out var line);
        if (line is not null)
        {
            lines.Add(line);
        }

        return matched;
    }

    // A certificate refused without -k is curl's exit 60, whose message is the verify result:
    // it prints neither verify result line nor the pin's hash (measured with curl 8.18.0, BL-1178).
    private static bool CertificateRefused(TlsHandshakeEvent handshake) =>
        handshake.Failed && handshake.VerifiedHostName is not null && VerifyResultOf(handshake) != 0;

    private static long VerifyResultOf(TlsHandshakeEvent handshake) =>
        handshake.CertificateVerifyResult ?? (handshake.CertificateVerified ? 0 : UnspecifiedVerifyError);

    private static string CipherName(TlsCipherSuite? suite)
    {
        return suite is { } negotiated ? CipherNames.GetValueOrDefault(negotiated, negotiated.ToString()) : "(NONE)";
    }

    private static IEnumerable<string> VerifyResult(TlsHandshakeEvent handshake, bool isLibreSsl)
    {
        var result = VerifyResultOf(handshake);
        if (!isLibreSsl)
        {
            yield return "OpenSSL verify result: " + result.ToString("x", CultureInfo.InvariantCulture);
        }

        yield return result == 0 ? "SSL certificate verified via OpenSSL." : " SSL certificate verification failed, continuing anyway!";
    }
}

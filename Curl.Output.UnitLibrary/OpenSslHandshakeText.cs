using System.Globalization;
using System.Net.Security;
using System.Security.Authentication;
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
        var lines = new List<string>();
        lines.AddRange(alpnLines.Take(1));
        lines.Add(
            $"SSL connection using {VersionNames.GetValueOrDefault(handshake.ProtocolVersion, "unknown")} / " +
            $"{CipherName(handshake.CipherSuite)} / {handshake.NegotiatedGroupName ?? "[blank]"} / {handshake.PeerSignatureTypeName ?? "UNDEF"}");
        lines.AddRange(alpnLines.Skip(1));
        if (handshake.ServerCertificate is { } certificate)
        {
            lines.AddRange(OpenSslCertificateText.ServerCertificate(certificate));
            lines.AddRange(handshake.PeerCertificateChain
                .Select((chainCertificate, level) => OpenSslCertificateText.CertificateLevel(level, chainCertificate))
                .OfType<string>());
            lines.AddRange(VerifyResult(handshake));
        }

        return lines;
    }

    private static string CipherName(TlsCipherSuite? suite)
    {
        return suite is { } negotiated ? CipherNames.GetValueOrDefault(negotiated, negotiated.ToString()) : "(NONE)";
    }

    private static IEnumerable<string> VerifyResult(TlsHandshakeEvent handshake)
    {
        var result = handshake.CertificateVerifyResult ?? (handshake.CertificateVerified ? 0 : UnspecifiedVerifyError);
        yield return "OpenSSL verify result: " + result.ToString("x", CultureInfo.InvariantCulture);
        yield return result == 0 ? "SSL certificate verified via OpenSSL." : " SSL certificate verification failed, continuing anyway!";
    }
}

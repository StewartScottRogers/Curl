using Curl.Api.Info;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Fills the <see cref="TransferInfo" /> <c>curl_easy_getinfo</c> reports from a finished transfer's
/// <see cref="TransferResult" /> and its <see cref="TransferReport" /> (ADR-0506, BL-2317): every
/// <c>CURLINFO_*</c> the engine measures, the part the <c>-w</c> variables read filled by
/// <c>Curl.Output</c>'s <see cref="WriteOutTransferInfo" />, the rest worked out here. A value the report does not hold
/// keeps <see cref="TransferInfo" />'s fresh-handle default.
/// </summary>
internal static class TransferInfoFromReport
{
    /// <summary>The line a certificate's PEM block starts with.</summary>
    private const string PemBeginLine = "-----BEGIN CERTIFICATE-----";

    /// <summary>The <c>CURLAUTH_*</c> bit of each scheme name a challenge starts with.</summary>
    private static readonly Dictionary<string, long> CurlAuthBitsByChallengeScheme = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1,
        ["Digest"] = 2,
        ["Negotiate"] = 4,
        ["NTLM"] = 8,
        ["Bearer"] = 64,
    };

    /// <summary>The <c>CURLPROTO_*</c> bit of each scheme, as curl 8.22.0's <c>curl.h</c> numbers them.</summary>
    private static readonly Dictionary<string, long> CurlProtoBitsByScheme = new(StringComparer.Ordinal)
    {
        ["http"] = 1L << 0,
        ["https"] = 1L << 1,
        ["ftp"] = 1L << 2,
        ["ftps"] = 1L << 3,
        ["scp"] = 1L << 4,
        ["sftp"] = 1L << 5,
        ["telnet"] = 1L << 6,
        ["ldap"] = 1L << 7,
        ["ldaps"] = 1L << 8,
        ["dict"] = 1L << 9,
        ["file"] = 1L << 10,
        ["tftp"] = 1L << 11,
        ["imap"] = 1L << 12,
        ["imaps"] = 1L << 13,
        ["pop3"] = 1L << 14,
        ["pop3s"] = 1L << 15,
        ["smtp"] = 1L << 16,
        ["smtps"] = 1L << 17,
        ["rtsp"] = 1L << 18,
        ["gopher"] = 1L << 25,
        ["smb"] = 1L << 26,
        ["smbs"] = 1L << 27,
        ["mqtt"] = 1L << 28,
        ["gophers"] = 1L << 29,
        ["ws"] = 1L << 30,
        ["wss"] = 1L << 31,
    };

    /// <summary>
    /// The info of one finished transfer.
    /// </summary>
    /// <param name="result">The finished transfer.</param>
    /// <param name="requestUrl">The URL the transfer was given, the effective URL when the report names no other.</param>
    /// <param name="fileTimeRequested">Whether <c>CURLOPT_FILETIME</c> asked for the remote file's time, without which libcurl answers -1.</param>
    /// <param name="timeProvider">The provider that took the report's <see cref="TransferTimings" />.</param>
    /// <param name="certificateInfoRequested">Whether <c>CURLOPT_CERTINFO</c> asked for the peer certificates' records, without which libcurl's <c>CURLINFO_CERTINFO</c> is empty.</param>
    /// <returns>The info.</returns>
    internal static TransferInfo Of(TransferResult result, string requestUrl, bool fileTimeRequested, TimeProvider timeProvider, bool certificateInfoRequested = false)
    {
        TransferReport report = result.Report ?? new TransferReport();
        string effectiveUrl = report.EffectiveUrl ?? requestUrl;
        TransferInfo info = WithCounts(WriteOutTransferInfo.FromReport(result, requestUrl, timeProvider), result, report);
        return result.Report is null ? info : info with
        {
            Scheme = SchemeOf(effectiveUrl),
            Protocol = ProtocolOf(effectiveUrl),
            FileTime = fileTimeRequested ? result.SourceLastWriteUnixSeconds ?? -1 : -1,
            RetryAfter = RetryAfterOf(report, timeProvider),
            CertInfo = CertInfoOf(report, certificateInfoRequested),
        };
    }

    /// <summary>
    /// The <c>CURLINFO_CERTINFO</c> lists of the report's peer certificates, the server's own first; none unless
    /// <c>CURLOPT_CERTINFO</c> <paramref name="requested" /> them.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> CertInfoOf(TransferReport report, bool requested) =>
        requested ? [.. report.PeerCertificates.Select(certificate => CertInfoRecordsOf(PeerCertificateText.Format(certificate)))] : [];

    /// <summary>
    /// One certificate's <c>CURLINFO_CERTINFO</c> list, from its <c>%{certs}</c> text (<see cref="PeerCertificateText" />):
    /// each <c>label:value</c> line is one string, and the PEM block, which <c>%{certs}</c> prints without the label,
    /// is libcurl's last string, <c>Cert:</c> and the block with its line feeds. Empty when the text is (a certificate
    /// curl could not read).
    /// </summary>
    /// <param name="certificateText">The certificate's <c>%{certs}</c> text.</param>
    /// <returns>The certificate's strings, in libcurl's order.</returns>
    internal static IReadOnlyList<string> CertInfoRecordsOf(string certificateText)
    {
        int pemStart = certificateText.IndexOf(PemBeginLine, StringComparison.Ordinal);
        return pemStart < 0
            ? []
            : [.. certificateText[..pemStart].Split('\n', StringSplitOptions.RemoveEmptyEntries), "Cert:" + certificateText[pemStart..]];
    }

    private static TransferInfo WithCounts(TransferInfo info, TransferResult result, TransferReport report) => info with
    {
        ConditionUnmet = result.TimeConditionUnmet ? 1 : 0,
        HttpAuthUsed = CurlAuthBitsOf(report.AuthSchemePicked),
        ProxyAuthUsed = CurlAuthBitsOf(report.ProxyAuthSchemePicked),
        RtspClientCSeq = report.RtspClientSequenceNumber,
        RtspServerCSeq = report.RtspServerSequenceNumber,
        RtspCSeqReceived = report.RtspSequenceNumberReceived,
        HttpAuthAvailable = report.ResponseCode == 401 ? ChallengedCurlAuthBits(report, "WWW-Authenticate") : 0,
        ProxyAuthAvailable = report.ResponseCode == 407 ? ChallengedCurlAuthBits(report, "Proxy-Authenticate") : 0,
        ContentLengthDownload = ContentLengthOf(report),
        OsErrno = report.OsErrno,
        ProxyError = (long)report.ProxyError,
    };

    /// <summary>
    /// The last response's <c>Retry-After</c>, read as libcurl's <c>http_header_r</c> reads it whatever the
    /// response code (<see cref="RetryAfterHeader" />); zero when it has none.
    /// </summary>
    private static TimeSpan RetryAfterOf(TransferReport report, TimeProvider timeProvider) =>
        TimeSpan.FromSeconds(report.ResponseHeaders
            .Where(header => string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            .Select(header => RetryAfterHeader.ParseSeconds(header.Value, timeProvider.GetUtcNow()))
            .LastOrDefault());

    /// <summary>The <c>CURLAUTH_*</c> bits of the engine's schemes.</summary>
    private static long CurlAuthBitsOf(HttpAuthSchemes schemes) =>
        (schemes.HasFlag(HttpAuthSchemes.Basic) ? 1 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Digest) ? 2 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Negotiate) ? 4 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Ntlm) ? 8 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Bearer) ? 64 : 0);

    /// <summary>The <c>CURLAUTH_*</c> bits of the schemes the last response's <paramref name="headerName" /> lines offer.</summary>
    private static long ChallengedCurlAuthBits(TransferReport report, string headerName) =>
        report.ResponseHeaders
            .Where(header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
            .Select(header => CurlAuthBitsByChallengeScheme.GetValueOrDefault(header.Value.Trim().Split(' ')[0]))
            .Aggregate(0L, (bits, bit) => bits | bit);

    /// <summary>The last response's <c>Content-Length</c>; -1 when it has none, or none that parses.</summary>
    private static long ContentLengthOf(TransferReport report) =>
        report.ResponseHeaders
            .Where(header => string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(header => long.TryParse(header.Value.Trim(), out long length) ? length : -1)
            .LastOrDefault(-1);

    /// <summary>
    /// The scheme <c>%{scheme}</c> prints for a transfer of <paramref name="requestUrl" />: the URL's, in lower case, or
    /// <see langword="null" /> (printed as nothing) when the URL cannot be parsed or no handler serves its scheme, as
    /// curl 8.21.0 prints it for <c>dict://exa mple.com/</c> and <c>xyz://a/b</c>.
    /// </summary>
    /// <param name="requestUrl">The URL, with any <c>-G</c> / <c>--url-query</c> query.</param>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The scheme, or <see langword="null" />.</returns>
    internal static string? RequestSchemeOf(string requestUrl, TransferResult result) =>
        result.ExitCode != CurlExitCode.UnsupportedProtocol ? SchemeOf(requestUrl) : null;

    private static string? SchemeOf(string effectiveUrl) =>
        CurlUrl.TryParse(effectiveUrl, pathAsIs: false, out CurlUrl? url) ? url.Scheme : null;

    private static long ProtocolOf(string effectiveUrl) =>
        SchemeOf(effectiveUrl) is { } scheme ? CurlProtoBitsByScheme.GetValueOrDefault(scheme) : 0;
}

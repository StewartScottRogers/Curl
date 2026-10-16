using System.Net;
using Curl.Api.Info;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Fills the <see cref="TransferInfo" /> <c>curl_easy_getinfo</c> reports from a finished transfer's
/// <see cref="TransferResult" /> and its <see cref="TransferReport" /> (ADR-0506, BL-2317): every
/// <c>CURLINFO_*</c> the engine measures, each worked out as the <c>-w</c> variable of the same name
/// is in <c>Curl.Output</c>'s <c>TransferWriteOutVariables</c>. A value the report does not hold
/// keeps <see cref="TransferInfo" />'s fresh-handle default.
/// </summary>
internal static class TransferInfoFromReport
{
    private const long MicrosecondsPerSecond = 1_000_000;

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
    /// <returns>The info.</returns>
    internal static TransferInfo Of(TransferResult result, string requestUrl, bool fileTimeRequested, TimeProvider timeProvider)
    {
        TransferReport report = result.Report ?? new TransferReport();
        string effectiveUrl = report.EffectiveUrl ?? requestUrl;
        TransferInfo info = WithTimesAndSpeeds(WithEndPoints(WithCounts(WithTexts(new TransferInfo(), report, effectiveUrl), result, report), report), result, report, timeProvider);
        return result.Report is null ? info : info with
        {
            Scheme = SchemeOf(effectiveUrl),
            Protocol = ProtocolOf(effectiveUrl),
            FileTime = fileTimeRequested ? result.SourceLastWriteUnixSeconds ?? -1 : -1,
            RetryAfter = RetryAfterOf(report, timeProvider),
        };
    }

    private static TransferInfo WithTexts(TransferInfo info, TransferReport report, string effectiveUrl) => info with
    {
        EffectiveUrl = effectiveUrl,
        EffectiveMethod = report.Method ?? "GET",
        RedirectUrl = report.RedirectUrl,
        ContentType = report.ContentType,
        FtpEntryPath = report.FtpEntryPath,
        Referer = report.Referer,
    };

    private static TransferInfo WithCounts(TransferInfo info, TransferResult result, TransferReport report) => info with
    {
        ResponseCode = report.ResponseCode,
        HttpConnectCode = report.ProxyConnectResponseCode,
        HttpVersion = HttpVersionOf(report.HttpVersion),
        RedirectCount = report.RedirectCount,
        HeaderSize = report.HeaderSize,
        RequestSize = report.RequestSize,
        NumConnects = report.ConnectionCount,
        ConditionUnmet = result.TimeConditionUnmet ? 1 : 0,
        UsedProxy = report.UsedProxy ? 1 : 0,
        HttpAuthUsed = CurlAuthBitsOf(report.AuthSchemePicked),
        HttpAuthAvailable = report.ResponseCode == 401 ? ChallengedCurlAuthBits(report, "WWW-Authenticate") : 0,
        ProxyAuthAvailable = report.ResponseCode == 407 ? ChallengedCurlAuthBits(report, "Proxy-Authenticate") : 0,
        ContentLengthDownload = ContentLengthOf(report),
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

    private static TransferInfo WithEndPoints(TransferInfo info, TransferReport report) => info with
    {
        PrimaryIp = report.UnixSocketRemoteIp ?? AddressOf(report.RemoteEndPoint),
        PrimaryPort = report.RemoteEndPoint?.Port ?? 0,
        LocalIp = AddressOf(report.LocalEndPoint),
        LocalPort = report.LocalEndPoint?.Port ?? 0,
    };

    private static TransferInfo WithTimesAndSpeeds(TransferInfo info, TransferResult result, TransferReport report, TimeProvider timeProvider)
    {
        long downloadSize = result.Report?.DownloadSize ?? result.BytesTransferred;
        TransferInfo timed = report.Timings is { } timings ? WithTimes(info, timings, timeProvider) : info;
        long totalMicroseconds = timed.TotalTime.Ticks / TimeSpan.TicksPerMicrosecond;
        return timed with
        {
            SizeDownload = downloadSize,
            SizeUpload = report.UploadSize,
            SpeedDownload = BytesPerSecond(downloadSize, totalMicroseconds),
            SpeedUpload = BytesPerSecond(report.UploadSize, totalMicroseconds),
        };
    }

    private static TransferInfo WithTimes(TransferInfo info, TransferTimings timings, TimeProvider timeProvider)
    {
        ConnectTimings connect = timings.Connect ?? new ConnectTimings(timings.Started, null, null, null);
        return info with
        {
            TotalTime = TimeSinceStart(timings, timings.Completed, timeProvider),
            QueueTime = TimeSinceStart(timings, timings.Started, timeProvider),
            NameLookupTime = TimeSinceStart(timings, connect.NameResolved, timeProvider),
            ConnectTime = TimeSinceStart(timings, connect.Connected, timeProvider),
            AppConnectTime = TimeSinceStart(timings, connect.TlsHandshakeCompleted, timeProvider),
            PreTransferTime = TimeSinceStart(timings, timings.RequestReady, timeProvider),
            PostTransferTime = TimeSinceStart(timings, timings.RequestSent, timeProvider),
            StartTransferTime = TimeSinceStart(timings, timings.FirstByteReceived, timeProvider),
            RedirectTime = TimeSpan.FromMicroseconds(timings.RedirectDuration.Ticks / TimeSpan.TicksPerMicrosecond),
        };
    }

    /// <summary>
    /// The whole microseconds from the start to <paramref name="timestamp" />, at least one as curl's
    /// <c>Curl_pgrsTime</c> makes it; zero when the event did not happen.
    /// </summary>
    private static TimeSpan TimeSinceStart(TransferTimings timings, long? timestamp, TimeProvider timeProvider) =>
        TimeSpan.FromMicroseconds(timestamp is long reached
            ? Math.Max(1, timeProvider.GetElapsedTime(timings.Started, reached).Ticks / TimeSpan.TicksPerMicrosecond)
            : 0);

    /// <summary>
    /// Bytes per second as curl's <c>trspeed</c> computes it: truncated, without overflowing for a
    /// size too large to multiply by a million; 0 without timings.
    /// </summary>
    private static long BytesPerSecond(long size, long microseconds)
    {
        if (microseconds == 0)
        {
            return 0;
        }

        if (size < long.MaxValue / MicrosecondsPerSecond)
        {
            return size * MicrosecondsPerSecond / microseconds;
        }

        return microseconds >= MicrosecondsPerSecond ? size / (microseconds / MicrosecondsPerSecond) : long.MaxValue;
    }

    /// <summary>The <c>CURL_HTTP_VERSION_*</c> value of a status line's version; 0 for none.</summary>
    private static long HttpVersionOf(Version? version) => version switch
    {
        { Major: 1, Minor: 0 } => 1,
        { Major: 1 } => 2,
        { Major: 2 } => 3,
        { Major: 3 } => 30,
        _ => 0,
    };

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

    private static string AddressOf(IPEndPoint? endPoint) => endPoint?.Address.ToString() ?? string.Empty;

    private static string? SchemeOf(string effectiveUrl) =>
        CurlUrl.TryParse(effectiveUrl, pathAsIs: false, out CurlUrl? url) ? url.Scheme : null;

    private static long ProtocolOf(string effectiveUrl) =>
        SchemeOf(effectiveUrl) is { } scheme ? CurlProtoBitsByScheme.GetValueOrDefault(scheme) : 0;
}

using System.Net;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Fills the <see cref="TransferInfo" /> fields a finished transfer's <see cref="TransferReport" />
/// holds and the <c>-w</c> variables read, as <c>curl_easy_getinfo</c> answers them (ADR-0506,
/// BL-2319): the texts, the counts and sizes, the end points, the times and the speeds. A value the
/// report does not hold keeps <see cref="TransferInfo" />'s fresh-handle default. <c>Curl.Engine</c>'s
/// <c>TransferInfoFromReport</c> starts from it and adds the <c>CURLINFO_*</c> values no <c>-w</c>
/// variable reads.
/// </summary>
public static class WriteOutTransferInfo
{
    private const long MicrosecondsPerSecond = 1_000_000;

    /// <summary>
    /// The info of one finished transfer.
    /// </summary>
    /// <param name="result">The finished transfer.</param>
    /// <param name="requestUrl">The URL the transfer was given, the effective URL when the report names no other.</param>
    /// <param name="timeProvider">The provider whose <see cref="TimeProvider.GetTimestamp" /> took the report's <see cref="TransferTimings" />.</param>
    /// <returns>The info.</returns>
    /// <remarks>
    /// Without a report, <see cref="TransferInfo.SizeDownload" /> is
    /// <see cref="TransferResult.BytesTransferred" />. A time is the whole microseconds from
    /// <see cref="TransferTimings.Started" /> to its event, at least one as curl's
    /// <c>Curl_pgrsTime</c> makes it, and zero for an event that did not happen; a speed is the
    /// bytes per second over the total time, truncated, as curl's <c>trspeed</c> computes it.
    /// </remarks>
    public static TransferInfo FromReport(TransferResult result, string requestUrl, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(requestUrl);
        ArgumentNullException.ThrowIfNull(timeProvider);
        TransferReport report = result.Report ?? new TransferReport();
        return WithTimesAndSpeeds(WithEndPoints(WithCounts(WithTexts(new TransferInfo(), report, report.EffectiveUrl ?? requestUrl), report), report), result, report, timeProvider);
    }

    private static TransferInfo WithTexts(TransferInfo info, TransferReport report, string effectiveUrl) => info with
    {
        EffectiveUrl = effectiveUrl,
        EffectiveMethod = report.Method ?? "GET",
        RedirectUrl = report.RedirectUrl,
        ContentType = report.ContentType,
        FtpEntryPath = report.FtpEntryPath,
        Referer = report.Referer,
        RtspSessionId = report.RtspSessionId,
    };

    private static TransferInfo WithCounts(TransferInfo info, TransferReport report) => info with
    {
        ResponseCode = report.ResponseCode,
        HttpConnectCode = report.ProxyConnectResponseCode,
        HttpVersion = HttpVersionOf(report.HttpVersion),
        RedirectCount = report.RedirectCount,
        HeaderSize = report.HeaderSize,
        RequestSize = report.RequestSize,
        NumConnects = report.ConnectionCount,
        UsedProxy = report.UsedProxy ? 1 : 0,
        SslVerifyResult = report.SslVerifyResult,
        ProxySslVerifyResult = report.ProxySslVerifyResult,
    };

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

    private static string AddressOf(IPEndPoint? endPoint) => endPoint?.Address.ToString() ?? string.Empty;
}

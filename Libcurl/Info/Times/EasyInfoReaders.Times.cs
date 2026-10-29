using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.Info;

/// <summary>
/// The time, size and speed group's rows of <see cref="EasyInfoReaders"/>. A time's <c>_T</c> form is
/// its <see cref="TimeSpan"/> in whole microseconds, and its double form is that same microsecond
/// count divided by 1,000,000, as upstream's <c>getinfo.c</c> derives both from one microsecond value;
/// <see cref="CurlInfo.RetryAfter"/> is whole seconds. A size, speed or content length is the same
/// <see cref="long"/> for both forms, the double form converted (an unknown content length is -1 in
/// both). Each reader is a named method in an explicit delegate, not a lambda or a cached method group,
/// so the coverage report adds no complexity or branch to this method for them.
/// </summary>
internal static partial class EasyInfoReaders
{
    /// <summary>The divisor from the microsecond count to the double seconds form.</summary>
    private const double MicrosecondsPerSecond = 1_000_000;

    static partial void AddTimeReaders(Dictionary<CurlInfo, EasyInfoReader> readers)
    {
        readers[CurlInfo.TotalTime] = new EasyInfoReader(ReadTotalTime);
        readers[CurlInfo.TotalTimeT] = new EasyInfoReader(ReadTotalTimeT);
        readers[CurlInfo.NamelookupTime] = new EasyInfoReader(ReadNameLookupTime);
        readers[CurlInfo.NamelookupTimeT] = new EasyInfoReader(ReadNameLookupTimeT);
        readers[CurlInfo.ConnectTime] = new EasyInfoReader(ReadConnectTime);
        readers[CurlInfo.ConnectTimeT] = new EasyInfoReader(ReadConnectTimeT);
        readers[CurlInfo.AppconnectTime] = new EasyInfoReader(ReadAppConnectTime);
        readers[CurlInfo.AppconnectTimeT] = new EasyInfoReader(ReadAppConnectTimeT);
        readers[CurlInfo.PretransferTime] = new EasyInfoReader(ReadPreTransferTime);
        readers[CurlInfo.PretransferTimeT] = new EasyInfoReader(ReadPreTransferTimeT);
        readers[CurlInfo.PosttransferTimeT] = new EasyInfoReader(ReadPostTransferTimeT);
        readers[CurlInfo.StarttransferTime] = new EasyInfoReader(ReadStartTransferTime);
        readers[CurlInfo.StarttransferTimeT] = new EasyInfoReader(ReadStartTransferTimeT);
        readers[CurlInfo.RedirectTime] = new EasyInfoReader(ReadRedirectTime);
        readers[CurlInfo.RedirectTimeT] = new EasyInfoReader(ReadRedirectTimeT);
        readers[CurlInfo.QueueTimeT] = new EasyInfoReader(ReadQueueTimeT);
        readers[CurlInfo.RetryAfter] = new EasyInfoReader(ReadRetryAfter);
        readers[CurlInfo.EarlydataSentT] = new EasyInfoReader(ReadEarlyDataSentT);
        readers[CurlInfo.SizeUpload] = new EasyInfoReader(ReadSizeUpload);
        readers[CurlInfo.SizeUploadT] = new EasyInfoReader(ReadSizeUploadT);
        readers[CurlInfo.SizeDownload] = new EasyInfoReader(ReadSizeDownload);
        readers[CurlInfo.SizeDownloadT] = new EasyInfoReader(ReadSizeDownloadT);
        readers[CurlInfo.SizeDelivered] = new EasyInfoReader(ReadSizeDelivered);
        readers[CurlInfo.SpeedUpload] = new EasyInfoReader(ReadSpeedUpload);
        readers[CurlInfo.SpeedUploadT] = new EasyInfoReader(ReadSpeedUploadT);
        readers[CurlInfo.SpeedDownload] = new EasyInfoReader(ReadSpeedDownload);
        readers[CurlInfo.SpeedDownloadT] = new EasyInfoReader(ReadSpeedDownloadT);
        readers[CurlInfo.ContentLengthDownload] = new EasyInfoReader(ReadContentLengthDownload);
        readers[CurlInfo.ContentLengthDownloadT] = new EasyInfoReader(ReadContentLengthDownloadT);
        readers[CurlInfo.ContentLengthUpload] = new EasyInfoReader(ReadContentLengthUpload);
        readers[CurlInfo.ContentLengthUploadT] = new EasyInfoReader(ReadContentLengthUploadT);
    }

    /// <summary>The whole microseconds of <paramref name="time"/>, the value upstream's <c>_T</c> infos hold (finer ticks are dropped).</summary>
    /// <param name="time">The time to convert.</param>
    /// <returns>The microseconds.</returns>
    private static long Microseconds(TimeSpan time) => time.Ticks / TimeSpan.TicksPerMicrosecond;

    /// <summary>The seconds of <paramref name="time"/> as upstream's double infos give them: whole microseconds over 1,000,000.</summary>
    /// <param name="time">The time to convert.</param>
    /// <returns>The seconds.</returns>
    private static double Seconds(TimeSpan time) => Microseconds(time) / MicrosecondsPerSecond;

    private static object? ReadTotalTime(TransferInfo info, EasyOptions options) => Seconds(info.TotalTime);

    private static object? ReadTotalTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.TotalTime);

    private static object? ReadNameLookupTime(TransferInfo info, EasyOptions options) => Seconds(info.NameLookupTime);

    private static object? ReadNameLookupTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.NameLookupTime);

    private static object? ReadConnectTime(TransferInfo info, EasyOptions options) => Seconds(info.ConnectTime);

    private static object? ReadConnectTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.ConnectTime);

    private static object? ReadAppConnectTime(TransferInfo info, EasyOptions options) => Seconds(info.AppConnectTime);

    private static object? ReadAppConnectTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.AppConnectTime);

    private static object? ReadPreTransferTime(TransferInfo info, EasyOptions options) => Seconds(info.PreTransferTime);

    private static object? ReadPreTransferTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.PreTransferTime);

    private static object? ReadPostTransferTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.PostTransferTime);

    private static object? ReadStartTransferTime(TransferInfo info, EasyOptions options) => Seconds(info.StartTransferTime);

    private static object? ReadStartTransferTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.StartTransferTime);

    private static object? ReadRedirectTime(TransferInfo info, EasyOptions options) => Seconds(info.RedirectTime);

    private static object? ReadRedirectTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.RedirectTime);

    private static object? ReadQueueTimeT(TransferInfo info, EasyOptions options) => Microseconds(info.QueueTime);

    private static object? ReadRetryAfter(TransferInfo info, EasyOptions options) =>
        info.RetryAfter.Ticks / TimeSpan.TicksPerSecond;

    private static object? ReadEarlyDataSentT(TransferInfo info, EasyOptions options) => info.EarlyDataSent;

    private static object? ReadSizeUpload(TransferInfo info, EasyOptions options) => (double)info.SizeUpload;

    private static object? ReadSizeUploadT(TransferInfo info, EasyOptions options) => info.SizeUpload;

    private static object? ReadSizeDownload(TransferInfo info, EasyOptions options) => (double)info.SizeDownload;

    private static object? ReadSizeDownloadT(TransferInfo info, EasyOptions options) => info.SizeDownload;

    /// <summary>The bytes passed to the write callback, after content decoding and with the header lines under <c>CURLOPT_HEADER</c> (CGR-126).</summary>
    private static object? ReadSizeDelivered(TransferInfo info, EasyOptions options) => info.SizeDelivered;

    private static object? ReadSpeedUpload(TransferInfo info, EasyOptions options) => (double)info.SpeedUpload;

    private static object? ReadSpeedUploadT(TransferInfo info, EasyOptions options) => info.SpeedUpload;

    private static object? ReadSpeedDownload(TransferInfo info, EasyOptions options) => (double)info.SpeedDownload;

    private static object? ReadSpeedDownloadT(TransferInfo info, EasyOptions options) => info.SpeedDownload;

    private static object? ReadContentLengthDownload(TransferInfo info, EasyOptions options) =>
        (double)BodylessAwareContentLengthDownload(info);

    private static object? ReadContentLengthDownloadT(TransferInfo info, EasyOptions options) =>
        BodylessAwareContentLengthDownload(info);

    /// <summary>libcurl sets the body size to 0 for a 204 or 304 response, so an unknown length reads 0, not -1.</summary>
    private static long BodylessAwareContentLengthDownload(TransferInfo info) =>
        info.ContentLengthDownload < 0 && info.ResponseCode is 204 or 304 ? 0 : info.ContentLengthDownload;

    private static object? ReadContentLengthUpload(TransferInfo info, EasyOptions options) =>
        (double)info.ContentLengthUpload;

    private static object? ReadContentLengthUploadT(TransferInfo info, EasyOptions options) =>
        info.ContentLengthUpload;
}

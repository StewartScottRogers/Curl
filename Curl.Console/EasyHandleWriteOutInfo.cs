using Curl.Api.Info;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Info;

namespace Curl.Console;

/// <summary>
/// What a serial transfer's easy handle answered to <c>curl_easy_getinfo</c> for the <c>-w</c> variables, read
/// before <c>curl_easy_cleanup</c> as curl's tool reads them in <c>tool_writeout.c</c> (ADR-0625, BL-2452), with
/// the report of the perform it describes.
/// </summary>
/// <param name="Info">
/// The handle's answers, as a <see cref="TransferInfo" />: every field a <c>-w</c> variable prints but the ones the
/// runner still answers itself (the scheme, <c>Referer</c>, both IDs, both verify results and the early data).
/// </param>
/// <param name="PerformedReport">The report of the handle's last perform, or <see langword="null" /> when it reported none.</param>
internal sealed record EasyHandleWriteOutInfo(TransferInfo Info, TransferReport? PerformedReport)
{
    /// <summary>
    /// Reads <paramref name="easy" />'s answers through <see cref="CurlEasy.GetInfo" />. Every info read is one
    /// <c>Libcurl</c> reads, so each answer is <c>CURLE_OK</c> and is not checked.
    /// </summary>
    /// <param name="easy">The transfer's handle, after its last perform.</param>
    /// <param name="performed">The result its last perform reported, or <see langword="null" />.</param>
    /// <returns>The handle's answers.</returns>
    internal static EasyHandleWriteOutInfo Read(CurlEasy easy, TransferResult? performed) => new(
        new TransferInfo
        {
            EffectiveUrl = Text(easy, CurlInfo.EffectiveUrl)!,
            EffectiveMethod = Text(easy, CurlInfo.EffectiveMethod)!,
            RedirectUrl = Text(easy, CurlInfo.RedirectUrl),
            ContentType = Text(easy, CurlInfo.ContentType),
            FtpEntryPath = Text(easy, CurlInfo.FtpEntryPath),
            ResponseCode = Number(easy, CurlInfo.ResponseCode),
            HttpConnectCode = Number(easy, CurlInfo.HttpConnectcode),
            HttpVersion = Number(easy, CurlInfo.HttpVersion),
            RedirectCount = Number(easy, CurlInfo.RedirectCount),
            HeaderSize = Number(easy, CurlInfo.HeaderSize),
            RequestSize = Number(easy, CurlInfo.RequestSize),
            NumConnects = Number(easy, CurlInfo.NumConnects),
            UsedProxy = Number(easy, CurlInfo.UsedProxy),
            PrimaryIp = Text(easy, CurlInfo.PrimaryIp)!,
            PrimaryPort = Number(easy, CurlInfo.PrimaryPort),
            LocalIp = Text(easy, CurlInfo.LocalIp)!,
            LocalPort = Number(easy, CurlInfo.LocalPort),
            TotalTime = Time(easy, CurlInfo.TotalTimeT),
            QueueTime = Time(easy, CurlInfo.QueueTimeT),
            NameLookupTime = Time(easy, CurlInfo.NamelookupTimeT),
            ConnectTime = Time(easy, CurlInfo.ConnectTimeT),
            AppConnectTime = Time(easy, CurlInfo.AppconnectTimeT),
            PreTransferTime = Time(easy, CurlInfo.PretransferTimeT),
            PostTransferTime = Time(easy, CurlInfo.PosttransferTimeT),
            StartTransferTime = Time(easy, CurlInfo.StarttransferTimeT),
            RedirectTime = Time(easy, CurlInfo.RedirectTimeT),
            SizeDownload = Number(easy, CurlInfo.SizeDownloadT),
            SizeUpload = Number(easy, CurlInfo.SizeUploadT),
            SpeedDownload = Number(easy, CurlInfo.SpeedDownloadT),
            SpeedUpload = Number(easy, CurlInfo.SpeedUploadT),
        },
        performed?.Report);

    /// <summary>
    /// Tells whether these answers describe <paramref name="result" />: the handle's last perform reported the very
    /// report <paramref name="result" /> carries, so a result the runner rewrote afterwards (a write failure's exit
    /// code) still reads them, while one that never reached the handle does not.
    /// </summary>
    /// <param name="result">The result the transfer's <c>-w</c> output describes.</param>
    /// <returns><see langword="true" /> when the answers are the transfer's.</returns>
    internal bool Describes(TransferResult result) =>
        PerformedReport is not null && ReferenceEquals(result.Report, PerformedReport);

    private static string? Text(CurlEasy easy, CurlInfo info) => (string?)Value(easy, info);

    private static long Number(CurlEasy easy, CurlInfo info) => (long)Value(easy, info)!;

    private static TimeSpan Time(CurlEasy easy, CurlInfo info) => TimeSpan.FromMicroseconds(Number(easy, info));

    private static object? Value(CurlEasy easy, CurlInfo info)
    {
        _ = easy.GetInfo(info, out object? value);
        return value;
    }
}

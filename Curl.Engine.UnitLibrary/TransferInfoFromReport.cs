using Curl.Api.Info;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Fills the <see cref="TransferInfo" /> <c>curl_easy_getinfo</c> reports from a finished transfer's
/// <see cref="TransferResult" /> and its <see cref="TransferReport" /> (ADR-0506, BL-2317): every
/// <c>CURLINFO_*</c> the engine measures, the part the <c>-w</c> variables read filled by
/// <c>Curl.Output</c>'s <see cref="WriteOutTransferInfo" />, the rest worked out here. A value the report does not hold
/// keeps <see cref="TransferInfo" />'s fresh-handle default. Each kind of value is worked out in its own partial file
/// (<c>TransferInfoFromReport.Auth.cs</c>, <c>.CertInfo.cs</c>, <c>.LengthAndCondition.cs</c>, <c>.RetryAfter.cs</c>,
/// <c>.Scheme.cs</c>), so a change to one touches only that file (BL-3251).
/// </summary>
internal static partial class TransferInfoFromReport
{
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

    private static TransferInfo WithCounts(TransferInfo info, TransferResult result, TransferReport report) => WithLengthAndCondition(WithAuth(info, report), result, report) with
    {
        RtspClientCSeq = report.RtspClientSequenceNumber,
        RtspServerCSeq = report.RtspServerSequenceNumber,
        RtspCSeqReceived = report.RtspSequenceNumberReceived,
        OsErrno = report.OsErrno,
        ProxyError = (long)report.ProxyError,
    };
}

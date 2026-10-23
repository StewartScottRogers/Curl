using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The info with the RTSP client, server and received <c>CSeq</c> numbers the report holds.
    /// </summary>
    /// <param name="info">The info so far.</param>
    /// <param name="report">The finished transfer's report.</param>
    /// <returns>The info with the RTSP numbers set.</returns>
    private static TransferInfo WithRtsp(TransferInfo info, TransferReport report) => info with
    {
        RtspClientCSeq = report.RtspClientSequenceNumber,
        RtspServerCSeq = report.RtspServerSequenceNumber,
        RtspCSeqReceived = report.RtspSequenceNumberReceived,
    };
}

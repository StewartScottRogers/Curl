using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal sealed partial class ConnectionEndPointRecorder
{
    /// <summary>The status of the proxy's reply to the transfer's last CONNECT, or <c>0</c>.</summary>
    private int proxyConnectResponseCode;

    /// <summary>The bytes of the proxy's replies to every CONNECT of the transfer.</summary>
    private long proxyConnectHeaderBytes;

    /// <summary>How many header lines the proxy's reply to the transfer's last CONNECT held.</summary>
    private int connectReplyHeaderCount;

    /// <summary>How many connects of the transfer the proxy replied to with a CONNECT reply of their own.</summary>
    private int connectReplyCount;

    /// <summary>
    /// Records the proxy's reply to a CONNECT, as a connect result carries it whether the tunnel opened or not: its
    /// status, the source of <c>%{http_connect}</c> for every scheme (BL-5893), and, unless the connection was reused,
    /// its bytes and header lines, which count towards <c>%{size_header}</c> and <c>%{num_headers}</c> (BL-6019). A later
    /// reply's status and header lines replace the earlier one's and its bytes are added; a connect with no reply status
    /// (no CONNECT) leaves what was recorded.
    /// </summary>
    /// <param name="connect">The connect's result.</param>
    internal void RecordProxyConnect(ConnectResult connect)
    {
        if (connect.ProxyConnectResponseCode == 0)
        {
            return;
        }

        proxyConnectResponseCode = connect.ProxyConnectResponseCode;
        if (!connect.IsReused)
        {
            proxyConnectHeaderBytes += connect.ProxyConnectHeaderBytes;
            connectReplyHeaderCount = connect.ConnectReplyHeadersStored;
            connectReplyCount++;
        }
    }

    /// <summary>Forgets the recorded CONNECT replies.</summary>
    private void ClearProxyConnect()
    {
        proxyConnectResponseCode = 0;
        proxyConnectHeaderBytes = 0;
        connectReplyHeaderCount = 0;
        connectReplyCount = 0;
    }

    /// <summary>
    /// Puts the recorded CONNECT replies on <paramref name="result" />'s report when the handler reported no CONNECT
    /// status, as only the HTTP handler does, building a report as <see cref="ReportOn" /> does when the handler returned
    /// none; <paramref name="result" /> itself when no CONNECT reply was recorded or the handler reported its own.
    /// </summary>
    private TransferResult WithProxyConnect(TransferResult result) =>
        proxyConnectResponseCode == 0 || result.Report is { ProxyConnectResponseCode: not 0 }
            ? result
            : result with
            {
                Report = WithConnectReplies(result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred }),
            };

    /// <summary>
    /// <paramref name="report" /> with the last CONNECT reply's status, every reply's bytes added to its header size and
    /// the last reply's header lines counted as <see cref="CountedConnectReplyHeaders" /> says (ADR-1418). A reply with
    /// header lines also removes <see cref="TransferReport.TransferCompleteStandInHeader" />, which curl does not count
    /// beside them.
    /// </summary>
    private TransferReport WithConnectReplies(TransferReport report)
    {
        IReadOnlyList<KeyValuePair<string, string>> ownPseudoHeaders =
            connectReplyHeaderCount > 0 && report.PseudoHeaders.SequenceEqual([TransferReport.TransferCompleteStandInHeader])
                ? []
                : report.PseudoHeaders;
        return report with
        {
            ProxyConnectResponseCode = proxyConnectResponseCode,
            HeaderSize = report.HeaderSize + proxyConnectHeaderBytes,
            PseudoHeaders = ownPseudoHeaders,
            ConnectReplyHeaderCount = CountedConnectReplyHeaders(ownPseudoHeaders.Count + report.ResponseHeaders.Count),
        };
    }

    /// <summary>
    /// The last CONNECT reply's header lines that curl still counts: all of them, except when the transfer had one reply
    /// and its handler received <paramref name="ownHeaderCount" /> headers of its own after it, which curl counts in
    /// their place.
    /// </summary>
    private int CountedConnectReplyHeaders(int ownHeaderCount) =>
        connectReplyCount == 1 && ownHeaderCount > 0 ? 0 : connectReplyHeaderCount;
}

using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal sealed partial class ConnectionEndPointRecorder
{
    /// <summary>The status of the proxy's reply to the transfer's last CONNECT, or <c>0</c>.</summary>
    private int proxyConnectResponseCode;

    /// <summary>
    /// Records the status of the proxy's reply to a CONNECT, as a connect result carries it whether the tunnel opened or
    /// not, the source of <c>%{http_connect}</c> for every scheme (BL-5893); a later CONNECT replaces it, and <c>0</c>
    /// (no CONNECT, or no reply status) leaves what was recorded.
    /// </summary>
    /// <param name="responseCode">The connect's <see cref="ConnectResult.ProxyConnectResponseCode" />.</param>
    internal void RecordProxyConnect(int responseCode)
    {
        if (responseCode != 0)
        {
            proxyConnectResponseCode = responseCode;
        }
    }

    /// <summary>Forgets the recorded CONNECT reply status.</summary>
    private void ClearProxyConnect() => proxyConnectResponseCode = 0;

    /// <summary>
    /// Puts the recorded CONNECT reply status on <paramref name="result" />'s report when the handler reported none, as
    /// only the HTTP handler does, building a report as <see cref="ReportOn" /> does when the handler returned none;
    /// <paramref name="result" /> itself when no CONNECT reply was recorded or the handler reported its own.
    /// </summary>
    private TransferResult WithProxyConnect(TransferResult result) =>
        proxyConnectResponseCode == 0 || result.Report is { ProxyConnectResponseCode: not 0 }
            ? result
            : result with
            {
                Report = (result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred }) with { ProxyConnectResponseCode = proxyConnectResponseCode },
            };
}

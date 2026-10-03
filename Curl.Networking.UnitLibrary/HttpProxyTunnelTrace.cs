using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Writes the <c>[HTTP-PROXY]</c> and <c>[H1-PROXY]</c> lines curl 8.21.0 writes around a CONNECT
/// tunnel through a plain HTTP proxy under <c>--trace-config http-proxy</c>, <c>h1-proxy</c>,
/// <c>proxy</c> or a named <c>all</c> (measured with <c>Record-CurlExchange.ps1</c>, BL-1193 Notes).
/// Each method writes one step's lines, each line only when its filter is traced. curl writes a
/// poll round's three lines each time it finds the reply not there yet, so the count is fixed to the
/// one a loopback proxy measured (ADR-0357's BL-1193 amendment).
/// </summary>
/// <param name="tracesHttpProxy">Whether the <c>[HTTP-PROXY]</c> lines are written.</param>
/// <param name="tracesH1Proxy">Whether the <c>[H1-PROXY]</c> lines are written.</param>
internal sealed class HttpProxyTunnelTrace(bool tracesHttpProxy, bool tracesH1Proxy)
{
    /// <summary>Writes the lines before <c>CONNECT: no ALPN negotiated</c>.</summary>
    /// <param name="events">The target's events.</param>
    public void ReportConnecting(ITransferEvents events) => WriteHttpProxy(events, "CONNECT");

    /// <summary>
    /// Writes the lines between <c>CONNECT: no ALPN negotiated</c> and the CONNECT's own lines
    /// (<c>Proxy auth using</c>, <c>Establishing HTTP proxy tunnel to</c>).
    /// </summary>
    /// <param name="events">The target's events.</param>
    public void ReportSubfilterInstalled(ITransferEvents events)
    {
        WriteHttpProxy(events, "installing subfilter for HTTP/1.1");
        WriteH1Proxy(events, "connect");
        WriteH1Proxy(events, "CONNECT start");
    }

    /// <summary>Writes the lines between <c>Establishing HTTP proxy tunnel to</c> and the CONNECT's head.</summary>
    /// <param name="events">The target's events.</param>
    public void ReportSending(ITransferEvents events)
    {
        WriteTunnelState(events, "connect");
        WriteH1Proxy(events, "CONNECT send");
    }

    /// <summary>Writes the lines between the CONNECT's head and the reply's, one poll round included.</summary>
    /// <param name="events">The target's events.</param>
    public void ReportReceiving(ITransferEvents events)
    {
        WriteTunnelState(events, "receive");
        WriteH1Proxy(events, "CONNECT receive");
        WriteHttpProxy(events, "CONNECT");
        WriteH1Proxy(events, "connect");
        WriteH1Proxy(events, "CONNECT receive");
    }

    /// <summary>
    /// Writes the lines after the reply's head: for a reply that opens the tunnel up to
    /// <c>established</c>, before <c>CONNECT phase completed</c>; for any other, up to <c>failed</c>.
    /// </summary>
    /// <param name="events">The target's events.</param>
    /// <param name="opensTunnel">Whether the reply opened the tunnel.</param>
    public void ReportResponse(ITransferEvents events, bool opensTunnel)
    {
        WriteTunnelState(events, "response");
        WriteH1Proxy(events, "CONNECT response");
        WriteTunnelState(events, opensTunnel ? "established" : "failed");
    }

    /// <summary>
    /// Writes the line after <c>CONNECT tunnel established</c>: curl moves the finished tunnel's
    /// state to <c>failed</c> as it clears it.
    /// </summary>
    /// <param name="events">The target's events.</param>
    public void ReportEstablished(ITransferEvents events) => WriteTunnelState(events, "failed");

    /// <summary>Writes the HTTP proxy filter's removal, after the setup filter's.</summary>
    /// <param name="events">The target's events.</param>
    public void ReportFilterRemoved(ITransferEvents events)
    {
        WriteHttpProxy(events, "removing connected setup filter");
        WriteHttpProxy(events, "destroy");
    }

    /// <summary>
    /// Writes the line the HTTP handler's ALPN query makes of the tunnel filter, after every setup
    /// filter's removal and before <c>using HTTP/1.x</c>.
    /// </summary>
    /// <param name="events">The target's events.</param>
    public void ReportAlpnQueried(ITransferEvents events) => WriteH1Proxy(events, "query ALPN");

    private void WriteTunnelState(ITransferEvents events, string state) => WriteH1Proxy(events, $"new tunnel state '{state}'");

    private void WriteHttpProxy(ITransferEvents events, string text)
    {
        if (tracesHttpProxy)
        {
            events.ReportInfo("[HTTP-PROXY] " + text);
        }
    }

    private void WriteH1Proxy(ITransferEvents events, string text)
    {
        if (tracesH1Proxy)
        {
            events.ReportInfo("[H1-PROXY] " + text);
        }
    }
}

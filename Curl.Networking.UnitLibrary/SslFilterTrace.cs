using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Writes the lines curl 8.21.0's SSL filter writes around a TLS handshake: <c>[SSL]</c> for the
/// origin's under <c>--trace-config ssl</c>, <c>network</c> or <c>all</c>, <c>[SSL-PROXY]</c> for an
/// HTTPS proxy's under <c>proxy</c> or a named <c>all</c> (measured with
/// <c>Record-CurlExchange.ps1</c>, BL-1287 Notes). curl writes a poll round's lines each time it
/// finds the handshake not done yet, so the count is fixed to the two a loopback server measured, a
/// failed handshake's to one, and the descriptor to the first socket's (ADR-0357's BL-1287
/// amendment). The rounds are written once the handshake is done, so they follow its <c>-v</c> lines.
/// </summary>
/// <param name="filterName">The filter's name in its lines, <c>SSL</c> or <c>SSL-PROXY</c>.</param>
/// <param name="traced">Whether the filter's lines are written.</param>
/// <param name="tunnelTrace">
/// The tunnel whose <c>[HTTP-PROXY] CONNECT</c> line each poll round of an HTTPS proxy's handshake
/// writes first; <see langword="null" /> for an origin's handshake.
/// </param>
internal sealed class SslFilterTrace(string filterName, bool traced, HttpProxyTunnelTrace? tunnelTrace = null)
{
    /// <summary>The poll rounds a successful handshake waits on loopback (measured, BL-1287 Notes).</summary>
    public const int HandshakePollRounds = 2;

    /// <summary>Writes the line before the handshake's trust lines.</summary>
    /// <param name="events">The target's events.</param>
    public void ReportConnecting(ITransferEvents events) => Write(events, "cf_connect()");

    /// <summary>
    /// Writes the lines after the handshake's: its poll rounds and its result, <c>done=1</c> for a
    /// handshake that succeeded, the exit code and <c>done=0</c> for one that failed.
    /// </summary>
    /// <param name="events">The target's events.</param>
    /// <param name="secured">The handshake's outcome.</param>
    public void ReportFinished(ITransferEvents events, ConnectResult secured)
    {
        bool succeeded = secured.Connection is not null;
        for (int round = 0; round < (succeeded ? HandshakePollRounds : 1); round++)
        {
            Write(events, "cf_connect() -> 0, done=0");
            Write(events, $"adjust_pollset, POLLIN fd={ConnectAttemptTraceEvents.FirstSocketDescriptor}");
            tunnelTrace?.ReportConnecting(events);
            Write(events, "cf_connect()");
        }

        Write(events, succeeded ? "cf_connect() -> 0, done=1" : $"cf_connect() -> {(int)secured.ExitCode}, done=0");
    }

    /// <summary>Writes the lines of an ALPN query the filter answers.</summary>
    /// <param name="events">The target's events.</param>
    /// <param name="applicationProtocol">What the handshake's ALPN agreed; <see langword="null" /> for nothing.</param>
    public void ReportAlpnQueried(ITransferEvents events, string? applicationProtocol)
    {
        Write(events, "query ALPN");
        Write(events, $"query ALPN: returning '{applicationProtocol ?? "(nil)"}'");
    }

    private void Write(ITransferEvents events, string text)
    {
        if (traced)
        {
            events.ReportInfo($"[{filterName}] {text}");
        }
    }
}

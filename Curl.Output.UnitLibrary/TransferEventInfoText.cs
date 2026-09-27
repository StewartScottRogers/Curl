using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's info-line wording for the structured transfer events, without the
/// <c>* </c> prefix and the line end, shared by <c>-v</c> and the trace dumps (ADR-0046).
/// </summary>
/// <remarks>
/// TLS facts are worded as the Schannel build words them: only the ALPN lines.
/// </remarks>
internal static class TransferEventInfoText
{
    /// <summary>Returns the <c>Established connection</c> line.</summary>
    /// <param name="opened">The facts about the connection.</param>
    /// <returns>The line.</returns>
    public static string ConnectionOpened(ConnectionOpenedEvent opened)
    {
        return $"Established connection to {opened.HostName} ({opened.RemoteEndPoint.Address} port {opened.RemoteEndPoint.Port}) " +
            $"from {opened.LocalEndPoint.Address} port {opened.LocalEndPoint.Port} ";
    }

    /// <summary>Returns the <c>Reusing existing</c> line.</summary>
    /// <param name="reused">The facts about the connection.</param>
    /// <returns>The line.</returns>
    public static string ConnectionReused(ConnectionReusedEvent reused)
    {
        return $"Reusing existing {reused.Scheme}: connection with {(reused.IsProxy ? "proxy" : "host")} {reused.HostName}";
    }

    /// <summary>Returns the ALPN lines, or none when no protocol was offered.</summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    /// <returns>The lines, in the order curl prints them.</returns>
    public static IReadOnlyList<string> TlsHandshake(TlsHandshakeEvent handshake)
    {
        if (handshake.OfferedApplicationProtocols.Count == 0)
        {
            return [];
        }

        return
        [
            "ALPN: curl offers " + string.Join(',', handshake.OfferedApplicationProtocols),
            handshake.NegotiatedApplicationProtocol is { } accepted
                ? "ALPN: server accepted " + accepted
                : "ALPN: server did not agree on a protocol. Uses default.",
        ];
    }
}

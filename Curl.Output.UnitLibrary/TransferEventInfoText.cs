using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's info-line wording for the structured transfer events, without the
/// <c>* </c> prefix and the line end, shared by <c>-v</c> and the trace dumps (ADR-0046).
/// </summary>
/// <remarks>
/// TLS facts are worded as the caller's <see cref="TlsBackend"/> words them (ADR-0085).
/// </remarks>
internal static class TransferEventInfoText
{
    /// <summary>Returns the <c>Established connection</c> line.</summary>
    /// <param name="opened">The facts about the connection.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// Through a Unix domain socket curl 8.21.0 prints <c>Established connection to &lt;path&gt;
    /// (&lt;path&gt; port 0) from  port 0 </c>, the second path cut to 45 characters (measured, BL-507).
    /// A transfer's second connection, such as FTP's passive data connection, is
    /// <c>Established 2nd connection</c> (measured, BL-944).
    /// </remarks>
    public static string ConnectionOpened(ConnectionOpenedEvent opened)
    {
        string established = opened.IsSecondConnection ? "Established 2nd connection" : "Established connection";
        if (opened.UnixSocketRemoteIp is { } unixSocketRemoteIp)
        {
            return $"{established} to {opened.HostName} ({unixSocketRemoteIp} port 0) from  port 0 ";
        }

        return $"{established} to {opened.HostName} ({opened.RemoteEndPoint.Address} port {opened.RemoteEndPoint.Port}) " +
            $"from {opened.LocalEndPoint.Address} port {opened.LocalEndPoint.Port} ";
    }

    /// <summary>Returns the <c>Reusing existing</c> line.</summary>
    /// <param name="reused">The facts about the connection.</param>
    /// <returns>The line.</returns>
    public static string ConnectionReused(ConnectionReusedEvent reused)
    {
        return $"Reusing existing {reused.Scheme}: connection with {(reused.IsProxy ? "proxy" : "host")} {reused.HostName}";
    }

    /// <summary>
    /// Returns the lines a curl build prints for a finished handshake: for Schannel the ALPN
    /// lines, none when no protocol was offered; for OpenSSL those and
    /// <see cref="OpenSslHandshakeText"/>'s. A QUIC handshake under Schannel gets curl.se's
    /// LibreSSL lines, since that is the build that speaks HTTP/3 on Windows (ADR-0144).
    /// </summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    /// <param name="tlsBackend">The curl build whose wording to use.</param>
    /// <returns>The lines, in the order curl prints them.</returns>
    public static IReadOnlyList<string> TlsHandshake(TlsHandshakeEvent handshake, TlsBackend tlsBackend)
    {
        var alpnLines = AlpnLines(handshake);
        if (tlsBackend == TlsBackend.OpenSsl)
        {
            return OpenSslHandshakeText.Lines(handshake, alpnLines);
        }

        return handshake.IsQuic ? OpenSslHandshakeText.LibreSslLines(handshake, alpnLines) : alpnLines;
    }

    /// <summary>
    /// Returns the <c>SSL Trust</c> lines a curl build prints before a handshake: the OpenSSL
    /// build's (<see cref="OpenSslTrustText"/>), which curl.se's LibreSSL build also prints for
    /// a QUIC connect on Windows (ADR-0144); the Schannel build prints none.
    /// </summary>
    /// <param name="trust">The trust the connection is set up with.</param>
    /// <param name="tlsBackend">The curl build whose wording to use.</param>
    /// <returns>The lines, in the order curl prints them.</returns>
    public static IReadOnlyList<string> TlsTrust(TlsTrustEvent trust, TlsBackend tlsBackend)
    {
        return tlsBackend == TlsBackend.OpenSsl || trust.IsQuic ? OpenSslTrustText.Lines(trust) : [];
    }

    private static IReadOnlyList<string> AlpnLines(TlsHandshakeEvent handshake)
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

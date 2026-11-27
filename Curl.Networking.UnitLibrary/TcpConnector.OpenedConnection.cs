using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // A plain HTTP connection dialled under TracesTcpFilter writes curl 8.21.0's [TCP] send and recv
    // lines for its I/O, and [TCP] query ALPN after the setup filters' removal, before the handler's
    // "using HTTP/1.x" (measured, BL-1195 Notes). A target that names its own TcpIoTrace, as FTP's
    // control and data connections do, is written as it says, with no query ALPN line (BL-1259 Notes).
    private ConnectResult OpenedInPlaintext(DialedSocket dialed, ConnectTarget target, ConnectTimings timings, int proxyConnectResponseCode)
    {
        var lines = !dialed.TracesTcpFilter ? null : target.TcpIoTrace ?? (target.PoolScheme == "http" ? TcpIoTraceConnection.HttpLines : null);
        var tracesHttpIo = ReferenceEquals(lines, TcpIoTraceConnection.HttpLines);
        var connection = lines is null ? dialed.Connection : new TcpIoTraceConnection(dialed.Connection, target.Events, lines);
        var opened = WithSetupFiltersRemoved(
            Opened(dialed, target.Events, connection, timings, proxyConnectResponseCode, peerCertificates: null, applicationProtocol: null),
            dialed, target.Events);
        WriteQueryAlpnLine(dialed, target, tracesHttpIo);
        return opened;
    }

    // curl 8.21.0 writes the PROXY line after any tunnel and before TLS, from the socket's own ends
    // (BL-616), its setup filter first reporting that it added the HAPROXY filter (BL-1160 Notes).
    private async ValueTask WriteHaproxyLineAsync(HaproxyProtocolHeader header, DialedSocket dialed, ITransferEvents events, CancellationToken cancellationToken)
    {
        if (TracesSetupFilter)
        {
            events.ReportInfo(HaproxyFilterAddedLine);
        }

        var line = header.Build(dialed.LocalEndPoint, dialed.RemoteEndPoint);
        await dialed.Connection.WriteAsync(line, cancellationToken).ConfigureAwait(false);

        // The TCP filter sits below the HAPROXY filter, so it traces the line's send like any other,
        // before Established connection, over TLS too (measured, BL-1253 Notes).
        if (dialed.TracesTcpFilter)
        {
            events.ReportInfo($"[TCP] send(len={line.Length}) -> 0, {line.Length}");
        }
    }

    // Under TracesHaproxyFilter a connection that wrote the PROXY line reports curl 8.21.0's
    // [HAPROXY] filter removal after Established connection and any [SETUP] removal (BL-1160 Notes),
    // and a connection dialled under TracesHappyEyeballsFilter the [HAPPY-EYEBALLS] filter's after it
    // (BL-1161 Notes).
    private ConnectResult WithSetupFiltersRemoved(ConnectResult opened, DialedSocket dialed, ITransferEvents events)
    {
        // The [HTTP-PROXY] filter's removal follows the [SETUP] filter's (measured, BL-1193 Notes).
        dialed.TunnelTrace?.ReportFilterRemoved(events);
        if (haproxyProtocol is not null && TracesHaproxyFilter)
        {
            events.ReportInfo("[HAPROXY] removing connected setup filter");
            events.ReportInfo("[HAPROXY] destroy");
        }

        if (dialed.TracesHappyEyeballsFilter)
        {
            events.ReportInfo(ConnectAttemptTraceEvents.RemovingLine);
            events.ReportInfo(ConnectAttemptTraceEvents.DestroyLine);
        }

        return opened;
    }

    // curl 8.22.0 writes an IPv6 literal host bracketed in the Established connection line (CGR-221).
    private static string BracketedIfIpv6Literal(string hostName, string? unixSocketRemoteIp)
    {
        return unixSocketRemoteIp is null && IPAddress.TryParse(hostName, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{hostName}]"
            : hostName;
    }

    /// <summary>
    /// Numbers a connection that is ready for the transfer, reports it opened as curl 8.21.0's
    /// <c>Established connection</c> line, after any tunnel and TLS handshake (BL-228), and
    /// returns it.
    /// </summary>
    private ConnectResult Opened(
        DialedSocket dialed,
        ITransferEvents events,
        IConnection connection,
        ConnectTimings timings,
        int proxyConnectResponseCode,
        IReadOnlyList<ReadOnlyMemory<byte>>? peerCertificates,
        string? applicationProtocol)
    {
        var connectionNumber = TakeConnectionNumber();
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = BracketedIfIpv6Literal(dialed.HostName, dialed.UnixSocketRemoteIp),
            RemoteEndPoint = dialed.RemoteEndPoint ?? UnspecifiedEndPoint,
            LocalEndPoint = dialed.LocalEndPoint ?? UnspecifiedEndPoint,
            UnixSocketRemoteIp = dialed.UnixSocketRemoteIp,
            ConnectionNumber = connectionNumber,
        });

        return ConnectResult.Connected(
            connection,
            timings,
            dialed.LocalEndPoint,
            proxyConnectResponseCode,
            peerCertificates,
            isReused: false,
            connectionNumber,
            applicationProtocol,
            unixSocketPath: dialed.UnixSocketPath,
            mappedHost: dialed.MappedDestination?.Host,
            mappedPort: dialed.MappedDestination?.Port ?? 0,
            lastSocketError: dialed.EarlierSocketError);
    }
}

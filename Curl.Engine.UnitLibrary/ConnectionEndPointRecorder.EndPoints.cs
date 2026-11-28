using System.Net;
using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal sealed partial class ConnectionEndPointRecorder
{
    // A sockaddr_un holds its path from byte 2, after the address family; an abstract name's
    // path starts with a NUL byte.
    private const int UnixSocketPathOffset = 2;

    /// <summary>
    /// The number of connections the transfer opened, every one the recording connectors
    /// reported: for FTP the control connection and each data connection.
    /// </summary>
    private int connectionsOpened;

    /// <summary>
    /// The number of connects that failed with their TCP connection open, as a TLS handshake the connect
    /// limit or <c>-m</c> ended, which curl 8.22.0 counts in a failed transfer's <c>%{num_connects}</c> (STD-231).
    /// </summary>
    private int connectionsFailedAfterOpening;

    /// <summary>
    /// Records a connect that failed after its TCP connection opened, which a failed transfer counts in
    /// <c>%{num_connects}</c> though no end point is reported, as curl 8.22.0 does (measured, STD-231).
    /// </summary>
    /// <param name="local">The local end point of the connection that was open, <see langword="null" /> when not known.</param>
    /// <param name="remote">The peer of the connection that was open, <see langword="null" /> when not known.</param>
    internal void RecordFailedAfterOpening(IPEndPoint? local = null, IPEndPoint? remote = null)
    {
        connectionsFailedAfterOpening++;
        if (failedRemoteEndPoint is null)
        {
            failedLocalEndPoint = local;
            failedRemoteEndPoint = remote;
        }
    }

    /// <summary>The local end point of the first connect that failed with its TCP connection open (EXT-097).</summary>
    private IPEndPoint? failedLocalEndPoint;

    /// <summary>The peer of the first connect that failed with its TCP connection open (EXT-097).</summary>
    private IPEndPoint? failedRemoteEndPoint;

    /// <summary>
    /// Gets or sets whether a failed transfer that recorded no connection reports the end points of a connect
    /// that failed with its TCP connection open, as an extended protocol's does and curl's own schemes' do not:
    /// curl prints <c>%{remote_ip}</c> empty for an https TLS handshake <c>-m</c> ends (measured, EXT-097).
    /// </summary>
    internal bool ReportsEndPointsOfFailedConnects { get; set; }

    /// <summary>Gets the connect timings of the transfer's first connection, or <see langword="null" /> when none was given.</summary>
    internal ConnectTimings? FirstConnectTimings { get; private set; }

    /// <summary>Gets the local end of the first datagram channel the transfer opened, or <see langword="null" /> when none was opened or it has none to report.</summary>
    internal IPEndPoint? DatagramLocalEndPoint { get; private set; }

    /// <summary>Gets the timestamp at which the first datagram channel opened, or <see langword="null" /> when none was opened.</summary>
    internal long? DatagramOpenedAt { get; private set; }

    /// <summary>
    /// Remembers the local end and the open time of the first datagram channel, which only the
    /// schemes that report them (not TFTP, whose unconnected socket curl reports as none) put on their report (BL-5493).
    /// </summary>
    /// <param name="local">The channel's local end point, or <see langword="null" />.</param>
    /// <param name="openedAt">The timestamp the channel opened at.</param>
    /// <returns><see langword="true" /> when this channel is the transfer's first datagram channel and was recorded; <see langword="false" /> when an earlier one was.</returns>
    internal bool RecordDatagramChannel(IPEndPoint? local, long openedAt)
    {
        if (DatagramOpenedAt is not null)
        {
            return false;
        }

        DatagramLocalEndPoint = local;
        DatagramOpenedAt = openedAt;
        return true;
    }

    /// <summary>
    /// Replaces the end points of the first datagram channel with those of the channel as it now
    /// carries the transfer, after it moved to the next resolved address (BL-5532). Only the channel
    /// that is the transfer's first datagram channel and its first recorded connection may call it
    /// (<c>EndPointRecordingDatagramConnector</c> wraps no other, BL-5536). Nothing changes when no
    /// datagram channel was recorded.
    /// </summary>
    /// <param name="remote">The channel's server end point now.</param>
    /// <param name="local">The channel's local end point now, or <see langword="null" />.</param>
    internal void ReRecordDatagramChannel(IPEndPoint? remote, IPEndPoint? local)
    {
        if (DatagramOpenedAt is null || remote is null)
        {
            return;
        }

        DatagramLocalEndPoint = local;
        remoteEndPoint = remote;
    }

    /// <summary>Whether the transfer''s first connection has been recorded.</summary>
    private bool connectionRecorded;

    /// <summary>The local end of the first connection, or <see langword="null" /> when unknown.</summary>
    private IPEndPoint? localEndPoint;

    /// <summary>The remote end of the first connection, or <see langword="null" /> when it is not an IP end point.</summary>
    private IPEndPoint? remoteEndPoint;

    /// <summary>
    /// The <c>%{remote_ip}</c> text of the first connection when it went through a Unix domain
    /// socket, or <see langword="null" />.
    /// </summary>
    private string? unixSocketRemoteIp;

    /// <summary>
    /// Records a connection the transfer opened, unless it already opened one.
    /// </summary>
    /// <param name="local">
    /// The local address and port, or <see langword="null" /> when the connection has none to
    /// report, as a UDP channel curl never connects has none.
    /// </param>
    /// <param name="remote">
    /// The peer's address and port, or the Unix domain socket the connection went through; any
    /// other end point, or <see langword="null" />, records nothing, as a connection with no
    /// known peer has nothing to report.
    /// </param>
    /// <param name="reused">
    /// Whether the connection came from the pool rather than being opened, which curl does not
    /// count in <c>%{num_connects}</c>.
    /// </param>
    /// <param name="timings">The connect's timings, kept for the first connection: the source of <c>%{time_connect}</c> for a handler that reports none.</param>
    /// <returns><see langword="true" /> when this connection became the transfer's first recorded connection.</returns>
    internal bool Record(IPEndPoint? local, EndPoint? remote, bool reused = false, ConnectTimings? timings = null)
    {
        if (remote is not (IPEndPoint or UnixDomainSocketEndPoint))
        {
            return false;
        }

        connectionsOpened += reused ? 0 : 1;
        if (connectionRecorded)
        {
            return false;
        }

        RecordFirstConnection(local, remote);
        FirstConnectTimings = timings;
        return true;
    }

    /// <summary>Keeps the end points of the transfer's first connection.</summary>
    /// <param name="local">The local end point the connection was made from.</param>
    /// <param name="remote">The connection's peer: an IP end point or a Unix domain socket.</param>
    private void RecordFirstConnection(IPEndPoint? local, EndPoint remote)
    {
        remoteEndPoint = remote as IPEndPoint;
        unixSocketRemoteIp = remote is UnixDomainSocketEndPoint unixSocket ? RemoteIpOf(unixSocket) : null;
        localEndPoint = local;
        connectionRecorded = true;
    }

    /// <summary>Forgets the transfer's connections.</summary>
    private void ClearEndPoints()
    {
        connectionsOpened = 0;
        connectionsFailedAfterOpening = 0;
        failedLocalEndPoint = null;
        failedRemoteEndPoint = null;
        connectionRecorded = false;
        FirstConnectTimings = null;
        localEndPoint = null;
        remoteEndPoint = null;
        unixSocketRemoteIp = null;
    }

    /// <summary>
    /// Puts the end points of the first connect that failed with its TCP connection open on a failed
    /// transfer's report when <see cref="ReportsEndPointsOfFailedConnects" /> is set and the report has none (EXT-097).
    /// </summary>
    private TransferResult WithEndPointsOfFailedConnect(TransferResult result)
    {
        if (!ReportsEndPointsOfFailedConnects || result.IsSuccess || failedRemoteEndPoint is null)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        return report.RemoteEndPoint is not null
            ? result
            : result with { Report = report with { LocalEndPoint = failedLocalEndPoint, RemoteEndPoint = failedRemoteEndPoint } };
    }

    /// <summary>The end points half of <see cref="ReportOn" />.</summary>
    private TransferResult WithEndPoints(TransferResult result)
    {
        if (!connectionRecorded)
        {
            return WithEndPointsOfFailedConnect(result);
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        if (report.LocalEndPoint is not null || report.RemoteEndPoint is not null || report.UnixSocketRemoteIp is not null)
        {
            return result;
        }

        return result with
        {
            Report = report with { LocalEndPoint = localEndPoint, RemoteEndPoint = remoteEndPoint, UnixSocketRemoteIp = unixSocketRemoteIp },
        };
    }

    /// <summary>
    /// Puts the number of connections the transfer opened on <paramref name="result" />'s report,
    /// the source of <c>%{num_connects}</c>, when the handler counted none itself, as every handler
    /// but HTTP's leaves it: curl 8.22.0 prints 1 for an smtp, dict, gopher, ws, rtsp or sftp
    /// transfer, 2 for an ftp download and 1 after a refused ws upgrade (CGR-107, BL-3961).
    /// </summary>
    /// <remarks>
    /// A failed transfer also counts each connect that failed after its TCP connection opened, even when the
    /// handler counts its own connections, giving a result without a report one (STD-231).
    /// </remarks>
    /// <param name="result">The result with the recorded end points on its report.</param>
    /// <param name="handlerCountsConnections">Whether the handler counted the connections it opened itself.</param>
    /// <returns><paramref name="result" /> with the count on its report, or itself when nothing was counted or the handler's report counted some.</returns>
    private TransferResult WithConnectionCount(TransferResult result, bool handlerCountsConnections)
    {
        int count = (handlerCountsConnections ? 0 : connectionsOpened) + (result.IsSuccess ? 0 : connectionsFailedAfterOpening);
        if (count == 0)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        return report.ConnectionCount > 0 ? result : result with { Report = report with { ConnectionCount = count } };
    }

    /// <summary>
    /// Returns the text curl 8.21.0 prints as <c>%{remote_ip}</c> for a connection through
    /// <paramref name="unixSocket" />: <see cref="UnixSocketAddress.RemoteIpText" />.
    /// </summary>
    /// <param name="unixSocket">The socket the connection went through.</param>
    /// <returns>The path cut to 45 characters, or empty for an abstract name.</returns>
    private static string RemoteIpOf(UnixDomainSocketEndPoint unixSocket)
    {
        var isAbstract = unixSocket.Serialize()[UnixSocketPathOffset] == 0;
        return new UnixSocketAddress(unixSocket.ToString(), isAbstract).RemoteIpText;
    }
}

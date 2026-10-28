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

    /// <summary>Whether the transfer's first connection has been recorded.</summary>
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
    internal void Record(IPEndPoint? local, EndPoint? remote, bool reused = false)
    {
        if (remote is not (IPEndPoint or UnixDomainSocketEndPoint))
        {
            return;
        }

        connectionsOpened += reused ? 0 : 1;
        if (!connectionRecorded)
        {
            RecordFirstConnection(local, remote);
        }
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
        connectionRecorded = false;
        localEndPoint = null;
        remoteEndPoint = null;
        unixSocketRemoteIp = null;
    }

    /// <summary>The end points half of <see cref="ReportOn" />.</summary>
    private TransferResult WithEndPoints(TransferResult result)
    {
        if (!connectionRecorded)
        {
            return result;
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
    /// <param name="result">The result with the recorded end points on its report.</param>
    /// <returns><paramref name="result" /> with the count on its report, or itself when nothing was recorded or the handler counted.</returns>
    private TransferResult WithConnectionCount(TransferResult result) =>
        connectionsOpened == 0 || result.Report!.ConnectionCount > 0
            ? result
            : result with { Report = result.Report with { ConnectionCount = connectionsOpened } };

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

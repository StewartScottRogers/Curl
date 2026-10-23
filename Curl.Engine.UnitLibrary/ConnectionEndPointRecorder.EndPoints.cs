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
    internal void Record(IPEndPoint? local, EndPoint? remote)
    {
        if (connectionRecorded)
        {
            return;
        }

        switch (remote)
        {
            case IPEndPoint ip:
                remoteEndPoint = ip;
                break;
            case UnixDomainSocketEndPoint unixSocket:
                unixSocketRemoteIp = RemoteIpOf(unixSocket);
                break;
            default:
                return;
        }

        localEndPoint = local;
        connectionRecorded = true;
    }

    /// <summary>Forgets the first connection's end points.</summary>
    private void ClearEndPoints()
    {
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

using System.Net;
using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Remembers the end points of the first connection a transfer opens, as the recording
/// connectors report them, and puts them on the transfer's report when its handler left them
/// out, the source of <c>%{local_ip}</c>, <c>%{local_port}</c>, <c>%{remote_ip}</c> and
/// <c>%{remote_port}</c> for every scheme (ADR-0119).
/// </summary>
/// <remarks>
/// The first connection is the one curl 8.21.0 reports: for FTP the control connection, not
/// the data connection opened after it (measured, BL-515 Notes). A connection through a Unix
/// domain socket has no address or ports, so it is reported as
/// <see cref="TransferReport.UnixSocketRemoteIp" /> alone (measured, BL-793 Notes). One
/// recorder serves the run's transfers one at a time; <see cref="EndPointReportingProtocolHandler" />
/// clears it before each.
/// </remarks>
internal sealed class ConnectionEndPointRecorder
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

    /// <summary>The operating system's error number of the last failed connect attempt, or <c>0</c>.</summary>
    private long osErrno;

    /// <summary>Why the last failed SOCKS proxy handshake failed, or <see cref="CurlProxyCode.Ok" />.</summary>
    private CurlProxyCode proxyError;

    /// <summary>The local end of the redirect hop before this one, or <see langword="null" />.</summary>
    private IPEndPoint? earlierLocalEndPoint;

    /// <summary>The remote end of the redirect hop before this one, or <see langword="null" />.</summary>
    private IPEndPoint? earlierRemoteEndPoint;

    /// <summary>The status code the redirect hop before this one received, or <c>0</c>.</summary>
    private int earlierResponseCode;

    /// <summary>The status code the report of the hop being run carried, or <c>0</c>.</summary>
    private int responseCode;

    /// <summary>
    /// Forgets what the previous transfer recorded, or - for a redirect's next hop - sets it aside as the earlier hop's,
    /// which a hop that fails before it connects reports in its place as libcurl 8.22.0 keeps them (BL-3314).
    /// </summary>
    /// <param name="followingRedirect">Whether the transfer about to run is a redirect's next hop.</param>
    internal void Clear(bool followingRedirect = false)
    {
        earlierLocalEndPoint = followingRedirect ? localEndPoint ?? earlierLocalEndPoint : null;
        earlierRemoteEndPoint = followingRedirect ? remoteEndPoint ?? earlierRemoteEndPoint : null;
        earlierResponseCode = followingRedirect ? Math.Max(responseCode, earlierResponseCode) : 0;
        responseCode = 0;
        connectionRecorded = false;
        localEndPoint = null;
        remoteEndPoint = null;
        unixSocketRemoteIp = null;
        osErrno = 0;
        proxyError = CurlProxyCode.Ok;
    }

    /// <summary>
    /// Records the socket error of a failed connect's last attempt as the operating system's error
    /// number, the transfer's <c>CURLINFO_OS_ERRNO</c> (BL-2374), and why its SOCKS proxy handshake
    /// failed, the transfer's <c>CURLINFO_PROXY_ERROR</c> (BL-2375); a later failure replaces each,
    /// and <see cref="SocketError.Success" /> or <see cref="CurlProxyCode.Ok" /> leaves what was recorded.
    /// </summary>
    /// <param name="lastSocketError">The failed connect's <see cref="ConnectResult.LastSocketError" />.</param>
    /// <param name="failedProxyError">The failed connect's <see cref="ConnectResult.ProxyError" />.</param>
    internal void RecordFailedConnect(SocketError lastSocketError, CurlProxyCode failedProxyError = CurlProxyCode.Ok)
    {
        if (lastSocketError != SocketError.Success)
        {
            osErrno = OsErrnoOf(lastSocketError);
        }

        if (failedProxyError != CurlProxyCode.Ok)
        {
            proxyError = failedProxyError;
        }
    }

    /// <summary>
    /// The operating system's number for <paramref name="socketError" />, as the platform's curl reports
    /// <c>CURLINFO_OS_ERRNO</c>: the Winsock code on Windows (the Schannel build, <c>WSAECONNREFUSED</c>
    /// 10061), <c>errno</c> elsewhere (the OpenSSL builds, <c>ECONNREFUSED</c> 111 on Linux and 61 on
    /// macOS), which the runtime's <c>SocketException.NativeErrorCode</c> maps to.
    /// </summary>
    /// <param name="socketError">The socket error.</param>
    /// <returns>The platform's error number.</returns>
    internal static long OsErrnoOf(SocketError socketError) => new SocketException((int)socketError).NativeErrorCode;

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

    /// <summary>
    /// Puts the recorded end points on <paramref name="result" />'s report when the handler
    /// reported neither end point itself; a handler that reported them, as the HTTP handler
    /// does, keeps its own.
    /// </summary>
    /// <param name="result">The transfer's result, as its handler returned it.</param>
    /// <returns>
    /// <paramref name="result" /> with the recorded end points on its report; a result without
    /// a report gets one carrying only the end points and <see cref="TransferResult.BytesTransferred" />
    /// as its <see cref="TransferReport.DownloadSize" />, which <c>%{size_download}</c> printed
    /// without a report. <paramref name="result" /> itself when nothing was recorded or the
    /// handler reported an end point.
    /// </returns>
    internal TransferResult ReportOn(TransferResult result)
    {
        responseCode = result.Report?.ResponseCode ?? 0;
        return WithEarlierHop(WithEndPoints(WithConnectFailure(result)));
    }

    /// <summary>
    /// Puts the earlier redirect hop's status code and end points on the report of a hop that failed before it connected.
    /// </summary>
    private TransferResult WithEarlierHop(TransferResult result)
    {
        if (connectionRecorded || result.IsSuccess || earlierRemoteEndPoint is null)
        {
            return result;
        }

        TransferReport report = result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred };
        return result with
        {
            Report = report with { ResponseCode = earlierResponseCode, LocalEndPoint = earlierLocalEndPoint, RemoteEndPoint = earlierRemoteEndPoint },
        };
    }

    /// <summary>
    /// Puts the recorded <see cref="TransferReport.OsErrno" /> and <see cref="TransferReport.ProxyError" />
    /// on <paramref name="result" />'s report, building one as <see cref="ReportOn" /> does when the
    /// handler returned none; <paramref name="result" /> itself when no connect attempt failed.
    /// </summary>
    private TransferResult WithConnectFailure(TransferResult result) => osErrno == 0 && proxyError == CurlProxyCode.Ok
        ? result
        : result with
        {
            Report = (result.Report ?? new TransferReport { DownloadSize = result.BytesTransferred }) with { OsErrno = osErrno, ProxyError = proxyError },
        };

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

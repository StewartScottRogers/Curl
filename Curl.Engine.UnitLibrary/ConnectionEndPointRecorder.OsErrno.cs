using System.Net.Sockets;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal sealed partial class ConnectionEndPointRecorder
{
    /// <summary>The operating system's error number of the last failed connect attempt, or <c>0</c>.</summary>
    private long osErrno;

    /// <summary>Why the last failed SOCKS proxy handshake failed, or <see cref="CurlProxyCode.Ok" />.</summary>
    private CurlProxyCode proxyError;

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

    /// <summary>Forgets the failed connect attempt's error numbers.</summary>
    private void ClearConnectFailure()
    {
        osErrno = 0;
        proxyError = CurlProxyCode.Ok;
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
}

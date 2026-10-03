using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// How curl 8.21.0 writes an FTP transfer's plain connections' I/O under <c>--trace-config tcp</c>
/// (measured, BL-1259 Notes; ADR-0357's BL-1259 amendment).
/// </summary>
internal static class FtpTcpIoTraces
{
    /// <summary>
    /// The control connection: <c>[TCP]</c>, every read <c>recv(len=900)</c>, curl's reply buffer, and
    /// no would-block line, as curl polls before it reads a reply. The one curl writes before the
    /// transfer's last reply depends on whether the server has sent it yet (it writes none after a
    /// listing), so none is written.
    /// </summary>
    public static readonly TcpIoTraceLines Control = new("TCP", 900, WritesWouldBlockReads: false);

    /// <summary>
    /// The data connection, curl's second: <c>[TCP-1]</c>, each read its buffer's length (the bytes
    /// still expected, at most 102400, as the session sizes its reads), and the would-block line of a
    /// read the server has not answered yet.
    /// </summary>
    public static readonly TcpIoTraceLines Data = new("TCP-1", ReceiveLength: null, WritesWouldBlockReads: true);

    /// <summary>
    /// The lines for a control connection that stays plain, <see cref="Control" />; <see langword="null" />
    /// for <c>ftps://</c> or a transfer that asks for TLS, whose TLS records are not FTP's reads.
    /// </summary>
    /// <param name="implicitTls">Whether the URL is <c>ftps://</c>.</param>
    /// <param name="tlsRequirement">What the transfer's <c>--ssl</c> options ask for.</param>
    /// <returns>The lines, or <see langword="null" /> to write none.</returns>
    public static TcpIoTraceLines? ForControl(bool implicitTls, FtpTlsRequirement tlsRequirement) =>
        implicitTls || tlsRequirement != FtpTlsRequirement.None ? null : Control;
}

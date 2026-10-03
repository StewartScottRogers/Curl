using System.Net.Sockets;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Words a socket error as curl 8.21.0's <c>curlx_strerror</c> does in the line its socket
/// filter writes for a failed send, <c>Send failure: &lt;text&gt;</c>: the Schannel build's
/// own Winsock table on Windows (the words <c>Recv failure</c> was measured with, ADR-0088),
/// the C library's <c>strerror</c> elsewhere, which is what .NET's own socket error message
/// is off Windows.
/// </summary>
internal static class TelnetSocketErrorText
{
    /// <summary>Words a socket error for the platform this process runs on.</summary>
    /// <param name="failure">The socket error.</param>
    /// <returns>The words curl's build for this platform uses.</returns>
    public static string Current(SocketException failure) => For(failure, OperatingSystem.IsWindows());

    /// <summary>Words a socket error as curl's build for a platform does.</summary>
    /// <param name="failure">The socket error.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>
    /// <c>Connection was reset</c> or <c>Connection was aborted</c> on Windows for those two
    /// errors; otherwise the error's own message.
    /// </returns>
    public static string For(SocketException failure, bool isWindows) =>
        (isWindows, failure.SocketErrorCode) switch
        {
            (true, SocketError.ConnectionReset) => "Connection was reset",
            (true, SocketError.ConnectionAborted) => "Connection was aborted",
            _ => failure.Message,
        };
}

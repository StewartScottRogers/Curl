using System.Collections.Frozen;
using System.Net.Sockets;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Words a failed socket read or write as curl 8.21.0's socket filter does:
/// <c>Recv failure: &lt;words&gt;</c> for exit 56 (<c>CURLE_RECV_ERROR</c>,
/// <c>lib/cf-socket.c</c> line 1618) and <c>Send failure: &lt;words&gt;</c> for exit 55
/// (<c>CURLE_SEND_ERROR</c>, <c>lib/cf-socket.c</c> line 1562).
/// </summary>
/// <remarks>
/// The words are <c>curlx_strerror</c>'s (<c>lib/curlx/strerr.c</c>): on Windows the Schannel
/// build's own Winsock table, <c>get_winsock_error</c> (lines 119-164 for the twelve errors this
/// type knows), and the error's own message for any other; elsewhere the C library's
/// <c>strerror</c>, which is what .NET's <see cref="SocketException.Message"/> already is off
/// Windows. Every protocol library words its socket failures through this one table.
/// </remarks>
public static class CurlSocketErrorText
{
    private static readonly FrozenDictionary<SocketError, string> WinsockWords = new Dictionary<SocketError, string>
    {
        [SocketError.NetworkDown] = "Network down",
        [SocketError.NetworkUnreachable] = "Network unreachable",
        [SocketError.NetworkReset] = "Network has been reset",
        [SocketError.ConnectionAborted] = "Connection was aborted",
        [SocketError.ConnectionReset] = "Connection was reset",
        [SocketError.NoBufferSpaceAvailable] = "No buffer space",
        [SocketError.NotConnected] = "Socket is not connected",
        [SocketError.Shutdown] = "Socket has been shut down",
        [SocketError.TimedOut] = "Timed out",
        [SocketError.ConnectionRefused] = "Connection refused",
        [SocketError.HostDown] = "Host down",
        [SocketError.HostUnreachable] = "Host unreachable",
    }.ToFrozenDictionary();

    /// <summary>Words a failed read as curl's build for the platform this process runs on does.</summary>
    /// <param name="failure">The read's failure.</param>
    /// <returns>
    /// <c>Recv failure: &lt;words&gt;</c> when the failure, or an exception inside it, is a
    /// <see cref="SocketException"/>; otherwise <see langword="null"/>, so the caller keeps its
    /// own text.
    /// </returns>
    public static string? ReceiveFailure(IOException failure) => ReceiveFailure(failure, OperatingSystem.IsWindows());

    /// <summary>Words a failed read as curl's build for a platform does.</summary>
    /// <param name="failure">The read's failure.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>
    /// <c>Recv failure: &lt;words&gt;</c> when the failure, or an exception inside it, is a
    /// <see cref="SocketException"/>; otherwise <see langword="null"/>.
    /// </returns>
    public static string? ReceiveFailure(IOException failure, bool isWindows) => Failure("Recv failure: ", failure, isWindows);

    /// <summary>Words a failed write as curl's build for the platform this process runs on does.</summary>
    /// <param name="failure">The write's failure.</param>
    /// <returns>
    /// <c>Send failure: &lt;words&gt;</c> when the failure, or an exception inside it, is a
    /// <see cref="SocketException"/>; otherwise <see langword="null"/>, so the caller keeps its
    /// own text.
    /// </returns>
    public static string? SendFailure(IOException failure) => SendFailure(failure, OperatingSystem.IsWindows());

    /// <summary>Words a failed write as curl's build for a platform does.</summary>
    /// <param name="failure">The write's failure.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>
    /// <c>Send failure: &lt;words&gt;</c> when the failure, or an exception inside it, is a
    /// <see cref="SocketException"/>; otherwise <see langword="null"/>.
    /// </returns>
    public static string? SendFailure(IOException failure, bool isWindows) => Failure("Send failure: ", failure, isWindows);

    /// <summary>Words a socket error as <c>curlx_strerror</c> in curl's build for a platform does.</summary>
    /// <param name="failure">The socket error.</param>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns>
    /// On Windows, <c>get_winsock_error</c>'s words for the twelve errors it shares with
    /// <see cref="SocketError"/>; otherwise the error's own message.
    /// </returns>
    public static string Words(SocketException failure, bool isWindows) =>
        isWindows && WinsockWords.TryGetValue(failure.SocketErrorCode, out var words) ? words : failure.Message;

    private static string? Failure(string prefix, IOException failure, bool isWindows)
    {
        var socketFailure = FindSocketException(failure);
        return socketFailure is null ? null : prefix + Words(socketFailure, isWindows);
    }

    private static SocketException? FindSocketException(Exception failure)
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketFailure)
            {
                return socketFailure;
            }
        }

        return null;
    }
}

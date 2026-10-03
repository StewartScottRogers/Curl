using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Turns a connection write or read that threw into the failed message curl 8.21.0 ends the
/// transfer with: <c>smb_send</c> and <c>smb_send_and_recv</c> return the socket filter's
/// error, and <c>lib/smb.c</c> closes the connection with "SMB: failed to communicate" -
/// exit 55 (<see cref="CurlExitCode.SendError" />) for a send, exit 56
/// (<see cref="CurlExitCode.RecvError" />) for a receive, with the filter's reset text or
/// <c>curl_easy_strerror</c>'s.
/// </summary>
internal static class SmbIoFailures
{
    /// <summary>Makes the exit 55 failure for a write or flush that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed message.</returns>
    public static SmbReceivedMessage SendFailed(IOException exception) =>
        SmbReceivedMessage.Failed(
            IsReset(exception) ? SmbMessages.SendConnectionReset : SmbMessages.SendFailed,
            CurlExitCode.SendError);

    /// <summary>Makes the exit 56 failure for a read that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed message.</returns>
    public static SmbReceivedMessage ReceiveFailed(IOException exception) =>
        SmbReceivedMessage.Failed(IsReset(exception) ? SmbMessages.ReceiveConnectionReset : SmbMessages.ReceiveFailed);

    private static bool IsReset(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset };
}

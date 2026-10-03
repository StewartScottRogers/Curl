using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// Turns a connection write or read that threw into the failed message curl 8.21.0 ends the
/// transfer with: <c>smb_send</c> and <c>smb_send_and_recv</c> return the socket filter's
/// error, and <c>lib/smb.c</c> closes the connection with "SMB: failed to communicate" -
/// exit 55 (<see cref="CurlExitCode.SendError" />) for a send, exit 56
/// (<see cref="CurlExitCode.RecvError" />) for a receive, with the filter's
/// <c>Send failure: &lt;words&gt;</c> or <c>Recv failure: &lt;words&gt;</c> for any socket
/// error, worded by <see cref="CurlSocketErrorText" /> (BL-1344), or
/// <c>curl_easy_strerror</c>'s text for a failure with no socket error in it.
/// </summary>
internal static class SmbIoFailures
{
    /// <summary>Makes the exit 55 failure for a write or flush that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed message.</returns>
    public static SmbReceivedMessage SendFailed(IOException exception) =>
        SmbReceivedMessage.Failed(
            CurlSocketErrorText.SendFailure(exception) ?? SmbMessages.SendFailed,
            CurlExitCode.SendError);

    /// <summary>Makes the exit 56 failure for a read that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed message.</returns>
    public static SmbReceivedMessage ReceiveFailed(IOException exception) =>
        SmbReceivedMessage.Failed(CurlSocketErrorText.ReceiveFailure(exception) ?? SmbMessages.ReceiveFailed);
}

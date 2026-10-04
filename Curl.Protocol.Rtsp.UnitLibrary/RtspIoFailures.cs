using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Turns a failed send, receive or header write into the exit code and message curl 8.21.0
/// gives it: 55 and 56 with <c>Send failure: &lt;words&gt;</c> / <c>Recv failure: &lt;words&gt;</c>
/// for any socket error, worded by <see cref="CurlSocketErrorText"/> (BL-1343), and 23 for a
/// header write.
/// </summary>
internal static class RtspIoFailures
{
    /// <summary>The exit 55 message for a failed write with no socket error in it.</summary>
    internal const string SendFailedMessage = "Failed sending data to the peer";

    /// <summary>The exit 56 message for a failed read with no socket error in it.</summary>
    internal const string ReceiveFailedMessage = "Failure when receiving data from the peer";

    /// <summary>Makes the exit 55 failure for a send that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static RtspTransferException SendFailed(IOException exception) =>
        new(CurlExitCode.SendError, CurlSocketErrorText.SendFailure(exception) ?? SendFailedMessage);

    /// <summary>Makes the exit 56 failure for a read that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static RtspTransferException ReceiveFailed(IOException exception) =>
        new(CurlExitCode.RecvError, CurlSocketErrorText.ReceiveFailure(exception) ?? ReceiveFailedMessage);

    /// <summary>Makes the exit 23 failure for a header write that threw <paramref name="exception" />.</summary>
    /// <param name="passed">How many bytes were passed to the write.</param>
    /// <param name="exception">What the stream threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static RtspTransferException WriteFailed(int passed, IOException exception)
    {
        int returned = exception is OutputWriteFailedException failure ? failure.BytesAccepted : 0;
        return new(
            CurlExitCode.WriteError,
            string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}"));
    }
}

using System.Globalization;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Turns a failed send, receive or <c>-D</c> write into the exit code and message curl 8.21.0
/// gives it: 55, 56 and 23, with the same text as the HTTP library. A <c>-D</c> write and a
/// payload write to the output fail alike, as curl writes both through one client writer.
/// </summary>
internal static class WsIoFailures
{
    /// <summary>The exit 55 message for a write the peer reset.</summary>
    internal const string SendConnectionReset = "Send failure: Connection was reset";

    /// <summary>The exit 55 message for any other failed write.</summary>
    internal const string SendFailedMessage = "Failed sending data to the peer";

    /// <summary>The exit 56 message for a read the peer reset.</summary>
    internal const string ReceiveConnectionReset = "Recv failure: Connection was reset";

    /// <summary>The exit 56 message for any other failed read.</summary>
    internal const string ReceiveFailedMessage = "Failure when receiving data from the peer";

    /// <summary>Makes the exit 55 failure for a send that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static WsTransferException SendFailed(IOException exception) =>
        new(CurlExitCode.SendError, IsReset(exception) ? SendConnectionReset : SendFailedMessage);

    /// <summary>Makes the exit 56 failure for a read that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static WsTransferException ReceiveFailed(IOException exception) =>
        new(CurlExitCode.RecvError, IsReset(exception) ? ReceiveConnectionReset : ReceiveFailedMessage);

    /// <summary>Makes the exit 23 failure for a <c>-D</c> or output write that threw <paramref name="exception" />.</summary>
    /// <param name="passed">How many bytes were passed to the write.</param>
    /// <param name="exception">What the stream threw.</param>
    /// <returns>The failure to throw.</returns>
    internal static WsTransferException WriteFailed(int passed, IOException exception)
    {
        int returned = exception is OutputWriteFailedException failure ? failure.BytesAccepted : 0;
        return new(
            CurlExitCode.WriteError,
            string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}"));
    }

    private static bool IsReset(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset };
}

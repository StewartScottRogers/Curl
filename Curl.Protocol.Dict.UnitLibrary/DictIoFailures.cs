using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Dict;

/// <summary>
/// Turns a failed send, receive or output write into the <see cref="TransferResult" /> curl
/// 8.21.0 gives it - exit 55, 56 and 23 with the texts the sibling handlers measured - and
/// names the <c>-v</c> lines that end such a transfer.
/// </summary>
internal static class DictIoFailures
{
    /// <summary>The exit 55 message for a failed write that carries no socket error.</summary>
    internal const string SendFailedMessage = "Failed sending data to the peer";

    /// <summary>The exit 56 message for a failed read that carries no socket error.</summary>
    internal const string ReceiveFailedMessage = "Failure when receiving data from the peer";

    /// <summary>The line <c>lib/dict.c</c> reports through <c>failf</c> after any failed send.</summary>
    internal const string DictRequestNotSent = "Failed sending DICT request";

    /// <summary>Makes the exit 55 result for a send that threw <paramref name="exception" />.</summary>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed transfer, with nothing written.</returns>
    internal static TransferResult SendFailed(IOException exception) =>
        TransferResult.Failure(CurlExitCode.SendError, CurlSocketErrorText.SendFailure(exception) ?? SendFailedMessage);

    /// <summary>Makes the exit 56 result for a read that threw <paramref name="exception" />.</summary>
    /// <param name="bytesWritten">The bytes written to the output before the read failed.</param>
    /// <param name="exception">What the connection threw.</param>
    /// <returns>The failed transfer.</returns>
    internal static TransferResult ReceiveFailed(long bytesWritten, IOException exception) =>
        new(CurlExitCode.RecvError, bytesWritten, CurlSocketErrorText.ReceiveFailure(exception) ?? ReceiveFailedMessage);

    /// <summary>Makes the exit 23 result for an output write that threw <paramref name="exception" />.</summary>
    /// <param name="bytesWritten">The bytes written to the output before this write.</param>
    /// <param name="passed">How many bytes were passed to the write.</param>
    /// <param name="exception">What the output threw.</param>
    /// <returns>The failed transfer.</returns>
    internal static TransferResult WriteFailed(long bytesWritten, int passed, IOException exception)
    {
        int returned = exception is OutputWriteFailedException failure ? failure.BytesAccepted : 0;
        return new(
            CurlExitCode.WriteError,
            bytesWritten,
            string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}"));
    }

    /// <summary>
    /// Whether <paramref name="message" /> is the text curl prints for a failed send or
    /// receive without calling <c>failf</c>, and so without a <c>-v</c> line of its own.
    /// </summary>
    /// <param name="message">The failure's message.</param>
    /// <returns><see langword="true" /> for the two fallback texts.</returns>
    internal static bool IsFallbackText(string message) =>
        message is SendFailedMessage or ReceiveFailedMessage;
}

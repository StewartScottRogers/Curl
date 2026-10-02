using System.Globalization;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Every failure message the <c>gopher</c> and <c>gophers</c> schemes report, as curl
/// 8.21.0 words it.
/// </summary>
/// <remarks>
/// <c>lib/gopher.c</c> fails without calling <c>failf</c> for a bad selector, so curl
/// prints the text <c>curl_easy_strerror</c> gives for the code, and that text is here.
/// </remarks>
internal static class GopherTransferMessages
{
    /// <summary>
    /// The exit 3 message for a selector that percent-decodes to a NUL byte.
    /// </summary>
    internal const string SelectorMalformed = "URL using bad/illegal format or missing URL";

    /// <summary>
    /// The exit 56 message curl falls back to for a failed receive.
    /// </summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>
    /// The exit 55 message curl falls back to for a failed send.
    /// </summary>
    internal const string SendFailed = "Failure when sending data to the peer";

    /// <summary>
    /// The exit 23 message for an output that stopped accepting bytes, as curl words it.
    /// </summary>
    /// <param name="passed">The number of bytes offered to the output: one read's worth.</param>
    /// <param name="returned">The number of those bytes the output accepted before it failed.</param>
    /// <returns>The message to report.</returns>
    internal static string OutputWriteFailed(int passed, int returned) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Failure writing output to destination, passed {passed} returned {returned}");

    /// <summary>
    /// The exit 23 message for a piece of the sent request the <c>-D</c> stream refused, as
    /// curl's client writer words a refused header write.
    /// </summary>
    /// <param name="passed">The length of the refused piece: the selector, or its CRLF.</param>
    /// <returns>The message to report.</returns>
    internal static string HeaderWriteFailed(int passed) =>
        string.Create(CultureInfo.InvariantCulture, $"client returned ERROR on write of {passed} bytes");

    /// <summary>
    /// The <c>-v</c> line that ends a finished transfer's connection, or one whose selector
    /// was malformed.
    /// </summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDownConnection(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>
    /// The <c>-v</c> line that ends a failed transfer's connection.
    /// </summary>
    /// <param name="connectionNumber">The connection's number.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string ClosingConnection(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}

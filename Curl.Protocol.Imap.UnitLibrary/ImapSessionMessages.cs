using System.Globalization;

namespace Curl.Protocol.Imap;

/// <summary>
/// The messages curl 8.21.0 prints when an IMAP session fails to open or to fetch, each measured with
/// <c>Record-CurlExchange.ps1 -Imap</c> (BL-553, BL-555, BL-557).
/// </summary>
internal static class ImapSessionMessages
{
    /// <summary>The server closed the connection before a response was complete (exit 56).</summary>
    internal const string ResponseReadingFailed = "response reading failed (errno: 0)";

    /// <summary>A response line reached 65536 bytes (exit 100).</summary>
    internal const string ResponseLineTooLarge = "A value or data field grew larger than allowed";

    /// <summary>The greeting was neither <c>* OK</c> nor <c>* PREAUTH</c> (exit 8).</summary>
    internal const string UnexpectedGreeting = "Got unexpected imap-server response";

    /// <summary>A <c>+</c> continuation arrived while no command wanted one (exit 8).</summary>
    internal const string UnexpectedContinuation = "Unexpected continuation response";

    /// <summary>A response line held a NUL byte (exit 8).</summary>
    internal const string NulByteInLine = "Nul byte in server response line";

    /// <summary>More bytes followed the tagged answer to <c>STARTTLS</c> in the same read (exit 8).</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>
    /// <c>--ssl-reqd</c>, and <c>CAPABILITY</c> did not advertise <c>STARTTLS</c>, was not
    /// answered <c>OK</c>, or the greeting was <c>PREAUTH</c> (exit 64).
    /// </summary>
    internal const string StartTlsNotAvailable = "STARTTLS not available.";

    /// <summary><c>--ssl-reqd</c> and <c>STARTTLS</c> was answered other than <c>OK</c> (exit 64).</summary>
    internal const string StartTlsDenied = "STARTTLS denied";

    /// <summary>The URL's path is malformed as an IMAP URL (exit 3); curl prints its error text.</summary>
    internal const string MalformedUrl = "URL using bad/illegal format or missing URL";

    /// <summary>
    /// <c>AUTHENTICATE</c> failed, or no login could be attempted with what the transfer has
    /// (exit 67); curl prints its error text.
    /// </summary>
    internal const string LoginDenied = "Login denied";

    /// <summary><c>SELECT</c> was answered other than <c>OK</c> (exit 67).</summary>
    internal const string SelectFailed = "Select failed";

    /// <summary>
    /// The URL's <c>UIDVALIDITY</c> is not the one <c>SELECT</c> reported (exit 78).
    /// </summary>
    internal const string UidValidityChanged = "Mailbox UIDVALIDITY has changed";

    /// <summary>
    /// <c>FETCH</c> completed with no untagged <c>FETCH</c> response, whatever its status
    /// (exit 78); curl prints its error text.
    /// </summary>
    internal const string RemoteFileNotFound = "Remote file not found";

    /// <summary>
    /// A <c>LIST</c>, a <c>SEARCH</c> or the <c>-X</c> command completed other than <c>OK</c>
    /// (exit 21); curl prints its error text.
    /// </summary>
    internal const string QuoteCommandFailed = "Quote command returned error";

    /// <summary>An upload's URL names no mailbox to <c>APPEND</c> to (exit 3).</summary>
    internal const string AppendWithoutMailbox = "Cannot APPEND without a mailbox.";

    /// <summary>An upload's size is not known before it is sent, as with <c>-T -</c> (exit 25).</summary>
    internal const string AppendWithUnknownSize = "Cannot APPEND with unknown input file size";

    /// <summary>
    /// <c>APPEND</c> was answered other than with a <c>+</c> continuation, or completed other
    /// than <c>OK</c> (exit 25); curl prints its error text.
    /// </summary>
    internal const string UploadFailed = "Upload failed (at start/before it took off)";

    /// <summary>The untagged <c>FETCH</c> response announced no literal <c>{n}</c> (exit 8).</summary>
    internal const string FetchResponseUnparsed = "Failed to parse FETCH response.";

    /// <summary>
    /// <c>LOGIN</c> was answered other than <c>OK</c> (exit 67). curl formats its response
    /// code with <c>%c</c>, so the message ends in a control character: 2 for <c>NO</c>,
    /// <c>BAD</c> and the like, 3 for <c>PREAUTH</c>.
    /// </summary>
    /// <param name="status">How the <c>LOGIN</c> completed.</param>
    /// <returns>The message, such as <c>Access denied. \u0002</c>.</returns>
    internal static string AccessDenied(ImapResponseStatus status) =>
        "Access denied. " + (status == ImapResponseStatus.Preauth ? '\u0003' : '\u0002');

    /// <summary>The server closed the connection <paramref name="missing" /> bytes short of the literal (exit 18).</summary>
    /// <param name="missing">The literal's bytes that never arrived.</param>
    /// <returns>The message, such as <c>end of response with 90 bytes missing</c>.</returns>
    internal static string LiteralCutShort(long missing) =>
        string.Create(CultureInfo.InvariantCulture, $"end of response with {missing} bytes missing");

    /// <summary>The output stopped accepting the message's bytes (exit 23).</summary>
    /// <param name="passed">The bytes offered to the output: one read's worth.</param>
    /// <param name="returned">The bytes of those the output accepted before it failed.</param>
    /// <returns>The message to report.</returns>
    internal static string OutputWriteFailed(int passed, int returned) =>
        string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}");
}

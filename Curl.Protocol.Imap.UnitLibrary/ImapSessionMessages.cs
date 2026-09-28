namespace Curl.Protocol.Imap;

/// <summary>
/// The messages curl 8.21.0 prints when an IMAP session fails to open, each measured with
/// <c>Record-CurlExchange.ps1 -Imap</c> (BL-553).
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
}

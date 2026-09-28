namespace Curl.Protocol.Pop3;

/// <summary>
/// The messages curl 8.21.0 prints when a POP3 session fails, each measured with
/// <c>Record-CurlExchange.ps1 -Pop3</c> (BL-547, BL-549).
/// </summary>
internal static class Pop3SessionMessages
{
    /// <summary>The server closed the connection before a response was complete (exit 56).</summary>
    internal const string ResponseReadingFailed = "response reading failed (errno: 0)";

    /// <summary>A response line reached 65536 bytes (exit 100).</summary>
    internal const string ResponseLineTooLarge = "A value or data field grew larger than allowed";

    /// <summary>The greeting was not <c>+OK</c> (exit 8).</summary>
    internal const string UnexpectedResponse = "Got unexpected pop3-server response";

    /// <summary><c>--ssl-reqd</c> and <c>CAPA</c> did not advertise <c>STLS</c>, or was refused (exit 64).</summary>
    internal const string StlsNotSupported = "STLS not supported.";

    /// <summary><c>--ssl-reqd</c> and <c>STLS</c> was answered with something other than <c>+OK</c> (exit 64).</summary>
    internal const string StartTlsDenied = "STARTTLS denied";

    /// <summary><c>LIST</c> or <c>RETR</c> was answered with something other than <c>+OK</c> (exit 8, BL-549).</summary>
    internal const string WeirdServerReply = "Weird server reply";

    /// <summary>The URL's message id decodes to a byte below 0x20 (exit 3, BL-549).</summary>
    internal const string UrlMalformed = "URL using bad/illegal format or missing URL";
}

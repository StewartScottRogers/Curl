namespace Curl.Protocol.Pop3;

/// <summary>
/// The messages curl 8.21.0 prints when a POP3 session fails, each measured with
/// <c>Record-CurlExchange.ps1 -Pop3</c> (BL-547, BL-548, BL-549).
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

    /// <summary>
    /// A SASL exchange failed, or no way of logging in was possible (exit 67, BL-548).
    /// </summary>
    internal const string LoginDenied = "Login denied";

    /// <summary>
    /// <c>USER</c> or <c>PASS</c> was refused; <c>{0}</c> is <c>-</c> for <c>-ERR</c>, <c>*</c>
    /// for another <c>+</c> line (exit 67, BL-548).
    /// </summary>
    internal const string AccessDenied = "Access denied. {0}";

    /// <summary>
    /// <c>APOP</c> was refused; <c>{0}</c> is 45 for <c>-ERR</c>, 42 for another <c>+</c> line
    /// (exit 67, BL-548).
    /// </summary>
    internal const string AuthenticationFailed = "Authentication failed: {0}";

    /// <summary>
    /// Tells whether curl 8.21.0's <c>-v</c> writes <paramref name="message" /> as a <c>*</c>
    /// line when the transfer fails with it (BL-552). It does for every message it formats
    /// itself, a TLS failure's included, and not for <see cref="WeirdServerReply" />,
    /// <see cref="UrlMalformed" />, <see cref="LoginDenied" /> and
    /// <see cref="ResponseLineTooLarge" />, which are only the exit code's own text.
    /// </summary>
    /// <param name="message">The failure's message.</param>
    /// <returns><see langword="true" /> when the message is written.</returns>
    internal static bool IsWrittenByVerbose(string message) =>
        message is not (WeirdServerReply or UrlMalformed or LoginDenied or ResponseLineTooLarge);
}

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

    /// <summary>A response line held a NUL byte (exit 8, BL-1120).</summary>
    internal const string NulByteInLine = "Nul byte in server response line";

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
    /// The <c>-v</c> line written before <see cref="LoginDenied" /> when no way of logging in
    /// was possible and <c>CAPA</c> listed no SASL mechanism curl knows, or was refused (BL-810).
    /// </summary>
    internal const string NoSaslMechanismOffered = "SASL: no auth mechanism was offered or recognized";

    /// <summary>
    /// The <c>-v</c> line written before <see cref="LoginDenied" /> when no way of logging in
    /// was possible although <c>CAPA</c> listed a SASL mechanism curl knows (BL-810).
    /// </summary>
    internal const string NoSaslMechanismOverlap = "SASL: no overlap between offered and configured auth mechanisms";

    /// <summary>
    /// The <c>-v</c> line written before <see cref="LoginDenied" /> when mechanisms were offered
    /// and allowed but none could be chosen, each then explained by the lines below (BL-810,
    /// BL-1221).
    /// </summary>
    internal const string NoSaslMechanismSelectable = "SASL: no auth mechanism offered could be selected";

    /// <summary>
    /// The <c>-v</c> line naming a mechanism, <c>{0}</c>, that was offered and allowed but is
    /// not built in, after <see cref="NoSaslMechanismSelectable" /> (BL-810).
    /// </summary>
    internal const string SaslMechanismNotBuiltIn = "SASL: {0} not builtin";

    /// <summary>
    /// The <c>-v</c> line after <see cref="NoSaslMechanismSelectable" /> when <c>AUTH=EXTERNAL</c>
    /// allowed EXTERNAL but a password was given (BL-1221).
    /// </summary>
    internal const string SaslExternalNotChosenWithPassword = "SASL: auth EXTERNAL not chosen with password";

    /// <summary>
    /// The <c>-v</c> line naming a bearer mechanism, <c>{0}</c>, that was offered and allowed
    /// without <c>--oauth2-bearer</c>, after <see cref="NoSaslMechanismSelectable" /> (BL-1221).
    /// </summary>
    internal const string SaslMechanismMissingBearer = "SASL: {0} is missing CURLOPT_XOAUTH2_BEARER";

    /// <summary>
    /// The <c>-v</c> line naming a mechanism, <c>{0}</c>, that was offered and allowed with an
    /// empty user name, after <see cref="NoSaslMechanismSelectable" /> (BL-1221).
    /// </summary>
    internal const string SaslMechanismMissingUserName = "SASL: {0} is missing username";

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

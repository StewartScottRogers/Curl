using System.Globalization;

namespace Curl.Protocol.Smtp;

/// <summary>
/// The messages curl 8.21.0 prints when an SMTP session fails to open, each measured with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-540).
/// </summary>
internal static class SmtpSessionMessages
{
    /// <summary>The server closed the connection before a reply was complete (exit 56).</summary>
    internal const string ResponseReadingFailed = "response reading failed (errno: 0)";

    /// <summary>A reply line reached 65536 bytes (exit 100).</summary>
    internal const string ReplyLineTooLarge = "A value or data field grew larger than allowed";

    /// <summary>The URL's path decoded to a control character (exit 3).</summary>
    internal const string MalformedUrl = "URL using bad/illegal format or missing URL";

    /// <summary><c>--ssl-reqd</c> and the <c>EHLO</c> reply did not advertise <c>STARTTLS</c> (exit 64).</summary>
    internal const string StartTlsNotSupported = "STARTTLS not supported.";

    /// <summary>The greeting was not a 2xx reply (exit 8).</summary>
    internal static string UnexpectedResponse(int code) =>
        string.Create(CultureInfo.InvariantCulture, $"Got unexpected smtp-server response: {code}");

    /// <summary><c>HELO</c>, or <c>EHLO</c> under <c>--ssl-reqd</c> before TLS, was refused (exit 9).</summary>
    internal static string RemoteAccessDenied(int code) =>
        string.Create(CultureInfo.InvariantCulture, $"Remote access denied: {code}");

    /// <summary><c>--ssl-reqd</c> and <c>STARTTLS</c> was answered with something other than 220 (exit 64).</summary>
    internal static string StartTlsDenied(int code) =>
        string.Create(CultureInfo.InvariantCulture, $"STARTTLS denied, code {code}");
}

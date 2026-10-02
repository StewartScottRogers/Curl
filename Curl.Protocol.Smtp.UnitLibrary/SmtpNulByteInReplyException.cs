namespace Curl.Protocol.Smtp;

/// <summary>
/// Ends an SMTP transfer when a reply line holds a NUL byte, which curl 8.21.0 refuses with
/// exit 8 before <c>-v</c> sees the line and without sending <c>QUIT</c> (BL-1121).
/// </summary>
internal sealed class SmtpNulByteInReplyException() : Exception(SmtpSessionMessages.NulByteInResponseLine);

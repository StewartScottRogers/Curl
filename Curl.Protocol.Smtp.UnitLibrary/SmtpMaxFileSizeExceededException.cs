namespace Curl.Protocol.Smtp;

/// <summary>
/// Ends the commands of an SMTP session with no message once the replies written reach
/// <c>--max-filesize</c>, which curl 8.21.0 fails with exit 63 after writing the replies up to
/// the limit, still sending <c>QUIT</c> (BL-1386).
/// </summary>
/// <param name="maxFileSize">The <c>--max-filesize</c> limit.</param>
/// <param name="written">The bytes written, the limit itself.</param>
internal sealed class SmtpMaxFileSizeExceededException(long maxFileSize, long written)
    : Exception(SmtpSessionMessages.MaxFileSizeExceeded(maxFileSize, written));

namespace Curl.Protocol.Smtp;

/// <summary>
/// One complete reply read from an SMTP connection: its three-digit code and every reply
/// line it was made of, continuation lines first and the final line last.
/// </summary>
/// <param name="Code">The code of the final line, such as 220 or 554.</param>
/// <param name="Lines">
/// Each reply line without its line end, such as <c>250-STARTTLS</c> and <c>250 SMTPUTF8</c>.
/// Lines that do not start with three digits are not reply lines and are not here, as curl
/// skips them.
/// </param>
internal sealed record SmtpReply(int Code, IReadOnlyList<string> Lines)
{
    /// <summary>
    /// Gets a value indicating whether <see cref="Code" /> is a 2xx completion.
    /// </summary>
    public bool IsCompletion => Code / 100 == 2;
}

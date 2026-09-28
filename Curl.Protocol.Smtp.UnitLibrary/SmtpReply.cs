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

    /// <summary>
    /// Gets the final reply line exactly as it arrived, with its line end (CRLF, or LF
    /// alone), which is what curl writes to the output for a command's reply (BL-543).
    /// </summary>
    public string FinalLine { get; init; } = string.Empty;

    /// <summary>
    /// Whether a line of this <c>EHLO</c> reply starts, after its code, with
    /// <paramref name="keyword" /> in any case, as curl 8.21.0 matches <c>STARTTLS</c>,
    /// <c>SIZE</c> and <c>SMTPUTF8</c> (measured: <c>250 size 100</c> and <c>250 SIZEX</c>
    /// both count as <c>SIZE</c>, BL-544).
    /// </summary>
    /// <param name="keyword">The extension's keyword in capitals, such as <c>SIZE</c>.</param>
    /// <returns><see langword="true" /> when some line advertises it.</returns>
    public bool Advertises(string keyword) =>
        Lines.Any(line => line.Length >= 4 + keyword.Length
            && line.AsSpan(4, keyword.Length).Equals(keyword, StringComparison.OrdinalIgnoreCase));
}

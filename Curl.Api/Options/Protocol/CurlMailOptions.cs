using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// SMTP's sending settings, mapped one to one onto <see cref="EasySmtpOptions"/> (BL-2503, ADR-0640).
/// Each member starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Mail"/> for every
/// request or on <see cref="CurlRequest.Mail"/> for one; a request's record replaces the client's whole.
/// IMAP and POP3 have no options of their own.
/// </summary>
public sealed record CurlMailOptions : IEasyOptionRecord<EasySmtpOptions>
{
    /// <summary><c>CURLOPT_MAIL_FROM</c>: the <c>MAIL FROM</c> address. Default <see langword="null"/>: blank.</summary>
    public string? MailFrom { get; init; }

    /// <summary><c>CURLOPT_MAIL_RCPT</c>: the <c>RCPT TO</c> addresses. Default none.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> MailRecipients
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(MailRecipients));
    } = [];

    /// <summary><c>CURLOPT_MAIL_AUTH</c>: the <c>AUTH</c> address. Default <see langword="null"/>.</summary>
    public string? MailAuth { get; init; }

    /// <summary><c>CURLOPT_MAIL_RCPT_ALLOWFAILS</c>: carry on when some recipients fail. Default <see langword="false"/>.</summary>
    public bool MailRecipientAllowFails { get; init; }

    /// <summary>An <see cref="EasySmtpOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's SMTP options.</returns>
    EasySmtpOptions IEasyOptionRecord<EasySmtpOptions>.ToEasy() => new()
    {
        MailFrom = MailFrom,
        MailRecipients = MailRecipients,
        MailAuth = MailAuth,
        MailRecipientAllowFails = MailRecipientAllowFails,
    };
}

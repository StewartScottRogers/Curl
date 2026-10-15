namespace Curl.Api.Options.Protocols;

/// <summary>The SMTP options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasySmtpOptions
{
    /// <summary>
    /// <c>CURLOPT_MAIL_FROM</c>: the sender address for <c>MAIL FROM</c>. Default
    /// <see langword="null"/>, meaning an empty sender (<c>&lt;&gt;</c>).
    /// </summary>
    public string? MailFrom { get; init; }

    /// <summary>
    /// <c>CURLOPT_MAIL_RCPT</c>: the recipients, one <c>RCPT TO</c> each. Default empty (libcurl's
    /// <see langword="null"/> list).
    /// </summary>
    public IReadOnlyList<string> MailRecipients { get; init; } = [];

    /// <summary>
    /// <c>CURLOPT_MAIL_AUTH</c>: the <c>AUTH</c> parameter of <c>MAIL FROM</c>, the identity the
    /// message is submitted for. Default <see langword="null"/>, meaning none is sent.
    /// </summary>
    public string? MailAuth { get; init; }

    /// <summary>
    /// <c>CURLOPT_MAIL_RCPT_ALLOWFAILS</c>: carry on when some recipients are refused, as long as
    /// one is accepted. Default <see langword="false"/>.
    /// </summary>
    public bool MailRecipientAllowFails { get; init; }
}

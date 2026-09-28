namespace Curl.Protocol.Abstractions;

/// <summary>
/// The mail-only options of one transfer, filled by the command-line layer and read only by
/// the SMTP, POP3 and IMAP handlers (ADR-0121).
/// </summary>
/// <remarks>
/// <see cref="ITransferContext.Mail" /> being <see langword="null" /> means the scheme is not
/// a mail scheme, and a mail handler treats it exactly as <c>new MailRequestOptions()</c>,
/// every member at curl's "not given" value. <c>-u</c>/<c>--user</c> stays on
/// <see cref="ITransferContext.Credentials" />, <c>--ssl</c>/<c>--ssl-reqd</c> on
/// <see cref="ITransferContext.SslLevel" />, <c>-l</c> on
/// <see cref="ITransferContext.ListOnly" /> and <c>-T</c> on
/// <see cref="ITransferContext.Upload" />; none is repeated here.
/// </remarks>
public sealed record MailRequestOptions
{
    /// <summary>
    /// Gets the reverse path from <c>--mail-from</c>, verbatim, or <see langword="null" />
    /// when it was not given.
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// Gets each <c>--mail-rcpt</c> value verbatim, in command-line order; empty when none was
    /// given.
    /// </summary>
    public IReadOnlyList<string> Recipients { get; init; } = [];

    /// <summary>
    /// Gets the <c>AUTH=</c> address from <c>--mail-auth</c>, verbatim, or
    /// <see langword="null" /> when it was not given.
    /// </summary>
    public string? Auth { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--mail-rcpt-allowfails</c> was given, so an SMTP
    /// handler carries on while at least one recipient is accepted.
    /// </summary>
    public bool RecipientAllowFails { get; init; }

    /// <summary>
    /// Gets each flag from <c>--upload-flags</c>, verbatim and in the order given, for an
    /// IMAP <c>APPEND</c>; empty when it was not given.
    /// </summary>
    public IReadOnlyList<string> UploadFlags { get; init; } = [];

    /// <summary>
    /// Gets the command from <c>-X</c>/<c>--request</c> for a mail scheme, verbatim, or
    /// <see langword="null" /> to let the handler choose its own command.
    /// </summary>
    public string? CustomCommand { get; init; }

    /// <summary>
    /// Gets the value of <c>--login-options</c>, verbatim, or <see langword="null" /> when it
    /// was not given; the handler then reads the URL's <c>;</c> options
    /// (<see cref="CurlUrl.Options" />), because the command line wins when both are present.
    /// </summary>
    public string? LoginOptions { get; init; }

    /// <summary>
    /// Gets the SASL authorization identity from <c>--sasl-authzid</c>, or
    /// <see langword="null" /> when it was not given.
    /// </summary>
    public string? SaslAuthorizationIdentity { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--sasl-ir</c> was given, so a SASL initial response
    /// may be sent on the authentication command line.
    /// </summary>
    public bool SaslInitialResponse { get; init; }

    /// <summary>
    /// Gets the OAuth 2.0 bearer token from <c>--oauth2-bearer</c>, or
    /// <see langword="null" /> when it was not given.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// Gets the service name from <c>--service-name</c>, or <see langword="null" /> for the
    /// scheme's default (<c>smtp</c>, <c>pop</c> or <c>imap</c>).
    /// </summary>
    public string? ServiceName { get; init; }
}

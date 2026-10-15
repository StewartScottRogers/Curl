using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Protocols;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Maps the mail options of a parsed command line onto the <see cref="MailRequestOptions" />
/// the SMTP, POP3 and IMAP handlers read (ADR-0121): <c>--mail-from</c>, <c>--mail-rcpt</c>,
/// <c>--mail-auth</c>, <c>--mail-rcpt-allowfails</c>, <c>--upload-flags</c>, <c>-X</c>,
/// <c>--login-options</c>, <c>--sasl-authzid</c>, <c>--sasl-ir</c> and <c>--oauth2-bearer</c>.
/// </summary>
internal static class MailRequestOptionsMapping
{
    /// <summary>The schemes whose transfers carry <see cref="MailRequestOptions" />, matched without case.</summary>
    private static readonly HashSet<string> MailSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "smtp", "smtps", "pop3", "pop3s", "imap", "imaps",
    };

    /// <summary>
    /// The schemes whose <c>-F</c> parts are sent as a MIME mail message, the upload, rather
    /// than as a form, matched without case (BL-1988).
    /// </summary>
    private static readonly HashSet<string> MimeMessageSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "smtp", "smtps", "imap", "imaps",
    };

    /// <summary>
    /// Tells whether <c>-F</c> parts to a URL of <paramref name="scheme" /> are a MIME mail
    /// message to upload, as curl 8.21.0 sends them to <c>smtp</c>, <c>smtps</c>, <c>imap</c>
    /// and <c>imaps</c> (BL-1988).
    /// </summary>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <returns><see langword="true" /> for those four schemes, without case.</returns>
    internal static bool SendsFormAsMimeMessage(string scheme) => MimeMessageSchemes.Contains(scheme);

    /// <summary>
    /// Each <c>--upload-flags</c> flag and the name it is sent under, in the fixed order curl
    /// 8.21.0 sends them whatever order they were given in (BL-535 Notes).
    /// </summary>
    private static readonly (ImapUploadFlags Flag, string Name)[] UploadFlagNames =
    [
        (ImapUploadFlags.Answered, "answered"),
        (ImapUploadFlags.Deleted, "deleted"),
        (ImapUploadFlags.Draft, "draft"),
        (ImapUploadFlags.Flagged, "flagged"),
        (ImapUploadFlags.Seen, "seen"),
    ];

    /// <summary>
    /// Copies the mail options from the option group's records and <paramref name="settings" /> for a transfer of
    /// <paramref name="scheme" />.
    /// </summary>
    /// <param name="settings">The tool settings, for <c>--upload-flags</c>.</param>
    /// <param name="transfer">The option group's transfer options, whose <see cref="EasyTransferOptions.CustomRequest" /> is the <c>-X</c> command.</param>
    /// <param name="smtp">The option group's SMTP options: <c>--mail-from</c>, <c>--mail-rcpt</c>, <c>--mail-auth</c> and <c>--mail-rcpt-allowfails</c>.</param>
    /// <param name="authentication">
    /// The option group's authentication options: <c>--login-options</c>, <c>--sasl-authzid</c>, <c>--sasl-ir</c>,
    /// <c>--oauth2-bearer</c> and <c>--service-name</c>.
    /// </param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <returns>
    /// <see langword="null" /> for every scheme but <c>smtp</c>, <c>smtps</c>, <c>pop3</c>,
    /// <c>pop3s</c>, <c>imap</c> and <c>imaps</c>; for those, every mail option verbatim, the
    /// sender, recipients (in command-line order), <c>AUTH</c> and allow-fails from <paramref name="smtp" />, and <see cref="EngineTransferSettings.UploadFlags" /> as
    /// the names of its set flags in curl's order. <see cref="MailRequestOptions.CustomCommand" /> is
    /// <see cref="EasyTransferOptions.CustomRequest" />; <see cref="MailRequestOptions.LoginOptions" />,
    /// <see cref="MailRequestOptions.SaslAuthorizationIdentity" /> and <see cref="MailRequestOptions.SaslInitialResponse" />
    /// are their <paramref name="authentication" /> members, <see cref="MailRequestOptions.BearerToken" /> is
    /// <see cref="EasyAuthenticationOptions.XOAuth2Bearer" />, and <see cref="MailRequestOptions.ServiceName" />
    /// is <see cref="EasyAuthenticationOptions.ServiceName" />, <see langword="null" /> for the scheme's default (ADR-0188).
    /// </returns>
    internal static MailRequestOptions? FromCommandLine(EngineTransferSettings settings, EasyTransferOptions transfer, EasySmtpOptions smtp, EasyAuthenticationOptions authentication, string scheme) =>
        !MailSchemes.Contains(scheme)
            ? null
            : new MailRequestOptions
            {
                From = smtp.MailFrom,
                Recipients = [.. smtp.MailRecipients],
                Auth = smtp.MailAuth,
                RecipientAllowFails = smtp.MailRecipientAllowFails,
                UploadFlags = UploadFlagNamesOf(settings.UploadFlags),
                CustomCommand = transfer.CustomRequest,
                LoginOptions = authentication.LoginOptions,
                SaslAuthorizationIdentity = authentication.SaslAuthorizationIdentity,
                SaslInitialResponse = authentication.SaslInitialResponse,
                BearerToken = authentication.XOAuth2Bearer,
                ServiceName = authentication.ServiceName,
            };

    /// <summary>Gets the names of the flags set in <paramref name="flags" />, in curl's order.</summary>
    /// <param name="flags">The <c>--upload-flags</c> bit set.</param>
    /// <returns>The names; empty when no flag is set.</returns>
    private static string[] UploadFlagNamesOf(ImapUploadFlags flags) =>
        [.. UploadFlagNames.Where(pair => flags.HasFlag(pair.Flag)).Select(pair => pair.Name)];
}

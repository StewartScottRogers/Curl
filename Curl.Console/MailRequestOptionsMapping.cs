using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

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
    /// Copies the mail options from <paramref name="options" /> for a transfer of
    /// <paramref name="scheme" />.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <param name="scheme">The transfer URL's scheme.</param>
    /// <returns>
    /// <see langword="null" /> for every scheme but <c>smtp</c>, <c>smtps</c>, <c>pop3</c>,
    /// <c>pop3s</c>, <c>imap</c> and <c>imaps</c>; for those, every mail option verbatim, the
    /// recipients in command-line order, and <see cref="CommandLineOptions.UploadFlags" /> as
    /// the names of its set flags in curl's order. <see cref="MailRequestOptions.ServiceName" />
    /// is <c>--service-name</c>, <see langword="null" /> for the scheme's default (ADR-0188).
    /// </returns>
    internal static MailRequestOptions? FromCommandLine(CommandLineOptions options, string scheme) =>
        !MailSchemes.Contains(scheme)
            ? null
            : new MailRequestOptions
            {
                From = options.MailFrom,
                Recipients = [.. options.MailRecipients],
                Auth = options.MailAuth,
                RecipientAllowFails = options.MailRecipientAllowFails,
                UploadFlags = UploadFlagNamesOf(options.UploadFlags),
                CustomCommand = options.RequestMethod,
                LoginOptions = options.LoginOptions,
                SaslAuthorizationIdentity = options.SaslAuthorizationIdentity,
                SaslInitialResponse = options.SaslInitialResponse,
                BearerToken = options.BearerToken,
                ServiceName = options.ServiceName,
            };

    /// <summary>Gets the names of the flags set in <paramref name="flags" />, in curl's order.</summary>
    /// <param name="flags">The <c>--upload-flags</c> bit set.</param>
    /// <returns>The names; empty when no flag is set.</returns>
    private static string[] UploadFlagNamesOf(ImapUploadFlags flags) =>
        [.. UploadFlagNames.Where(pair => flags.HasFlag(pair.Flag)).Select(pair => pair.Name)];
}

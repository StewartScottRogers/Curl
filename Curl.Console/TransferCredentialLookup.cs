using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Chooses a transfer's credentials from the URL's user information, and from the netrc file when
/// <c>-n</c>, <c>--netrc-file</c> or <c>--netrc-optional</c> asks, with curl 8.21.0's precedence
/// against <c>-u</c> (measured 2026-09-28, BL-505 and BL-791 Notes).
/// </summary>
/// <remarks>
/// <para>
/// A <c>-u</c> with a user name wins and no file is read, so a missing or broken file is not
/// noticed; <c>-u :</c> or <c>-u :pw</c> has no user name and does not count, so the URL's user
/// name and password replace it whole. With no netrc option, the URL's credentials are sent when
/// it gives a user name or a password (<see cref="CredentialsWrittenInUrl" />). Otherwise the file
/// is the <c>--netrc-file</c> one, or <c>.netrc</c> in <c>HOME</c>, with <c>_netrc</c> after it on
/// Windows, where <c>USERPROFILE</c> stands in when <c>HOME</c> is not set. A file that cannot be
/// read, a directory included, fails the transfer with <c>(26) .netrc error: no such file</c>
/// unless <c>--netrc-optional</c> is in effect, and a malformed one with
/// <c>(26) .netrc error: syntax error</c> on the same terms.
/// </para>
/// <para>
/// The URL's percent-decoded user name picks the entry (<see cref="NetrcFile" />). A matching
/// entry's login, else the URL's user name, else an empty one, is sent with the entry's
/// password, else the URL's, else an empty one: the entry's password beats the URL's. With no
/// entry, or no usable file under <c>--netrc-optional</c>, the URL's user name and password are
/// sent when it has a user name.
/// </para>
/// <para>
/// When the netrc file is in use, the lookup is made again for every redirect hop's URL
/// (<see cref="ForRedirectHops" />), so each hop sends its own host's entry, or none, under
/// <c>--location-trusted</c> too, as curl 8.21.0 does (measured, BL-505 Notes; BL-790).
/// </para>
/// </remarks>
/// <param name="fileReader">Reads the netrc file.</param>
/// <param name="readEnvironmentVariable">
/// Returns an environment variable's value, or <see langword="null" /> when it is not set; read
/// for <c>HOME</c> and, on Windows, <c>USERPROFILE</c>.
/// </param>
/// <param name="runsOnWindows">Whether the default location is looked for as curl's Windows build does.</param>
/// <param name="diagnosticLog">Where a matching netrc entry is logged, by host and login only (BL-1151); <see langword="null" /> logs nothing.</param>
internal sealed class TransferCredentialLookup(
    IDataFileReader fileReader,
    Func<string, string?> readEnvironmentVariable,
    bool runsOnWindows,
    IDiagnosticLog? diagnosticLog = null)
{
    /// <summary>The message curl 8.21.0 prints after <c>curl: (26) </c> when a required netrc file is missing.</summary>
    internal const string NoSuchFileMessage = ".netrc error: no such file";

    /// <summary>
    /// Looks up the credentials the transfer of <paramref name="url" /> sends.
    /// </summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="credentials">
    /// The credentials to send in place of <see cref="CommandLineOptions.Credentials" />;
    /// <see langword="null" /> when neither the URL nor the netrc file has anything to say, so the
    /// <c>-u</c> ones stand.
    /// </param>
    /// <param name="failure">The exit-26 failure the transfer ends with, when it fails.</param>
    /// <returns><see langword="false" /> when the transfer fails before it starts.</returns>
    internal bool TryLookUp(
        CommandLineOptions options,
        CurlUrl url,
        out NetworkCredential? credentials,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        credentials = null;
        failure = null;
        if (!string.IsNullOrEmpty(options.Credentials?.UserName))
        {
            return true;
        }

        if (options.NetrcUse == NetrcUse.Ignored)
        {
            credentials = CredentialsWrittenInUrl(url);
            return true;
        }

        string? urlUser = DecodedUserInformation(url.User);
        string? text = ReadNetrcText(options);
        NetrcLookupResult result = text is null ? NetrcLookupResult.NotFound : NetrcFile.Find(text, url.Host, urlUser, diagnosticLog);
        failure = FailureOf(options.NetrcUse, FailureMessageOf(text, result));
        credentials = CredentialsOf(result, urlUser, DecodedUserInformation(url.Password));
        return failure is null;
    }

    /// <summary>
    /// Gets the lookup each redirect hop's credentials come from when the netrc file is in use: the
    /// same lookup as the first URL's, for the hop's own URL, as curl 8.21.0 sends the
    /// <c>localhost</c> entry after a redirect from <c>127.0.0.1</c> to <c>localhost</c>, and nothing
    /// when <c>localhost</c> has no entry, <c>--location-trusted</c> or not (BL-790). A file problem
    /// on a hop sends the hop no netrc credentials rather than failing it.
    /// </summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <returns>
    /// The lookup, or <see langword="null" /> when no netrc option is in effect or a <c>-u</c> with a
    /// user name wins, so the first hop's credentials follow <c>RedirectFollower</c>'s origin rules.
    /// </returns>
    internal HopCredentialSelector? ForRedirectHops(CommandLineOptions options) =>
        options.NetrcUse == NetrcUse.Ignored || !string.IsNullOrEmpty(options.Credentials?.UserName)
            ? null
            : hopUrl =>
            {
                _ = TryLookUp(options, hopUrl, out NetworkCredential? credentials, out _);
                return credentials;
            };

    /// <summary>
    /// Gets the credentials written in the URL, sent when no netrc option is in effect: its
    /// percent-decoded user name and password, either one empty when absent, as curl 8.21.0 sends
    /// <c>zz:</c> for <c>http://zz@host/</c> and <c>:x</c> for <c>http://:x@host/</c> (BL-791 Notes).
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>
    /// The credentials, or <see langword="null" /> when the URL gives neither a user name nor a
    /// password, as <c>http://@host/</c> and <c>http://:@host/</c> send none.
    /// </returns>
    private static NetworkCredential? CredentialsWrittenInUrl(CurlUrl url)
    {
        string? user = DecodedUserInformation(url.User);
        string? password = DecodedUserInformation(url.Password);
        return user is null && password is null ? null : new NetworkCredential(user ?? string.Empty, password ?? string.Empty);
    }

    /// <summary>
    /// Gets what is wrong with the netrc file, whether or not it fails the transfer.
    /// </summary>
    /// <param name="text">The file's text, or <see langword="null" /> when none could be read.</param>
    /// <param name="result">The lookup in the file.</param>
    /// <returns>
    /// <see cref="NoSuchFileMessage" />, <see cref="NetrcLookupResult.SyntaxErrorMessage" />, or
    /// <see langword="null" /> when nothing is wrong.
    /// </returns>
    private static string? FailureMessageOf(string? text, NetrcLookupResult result)
    {
        if (text is null)
        {
            return NoSuchFileMessage;
        }

        return result.Outcome == NetrcLookupOutcome.SyntaxError ? NetrcLookupResult.SyntaxErrorMessage : null;
    }

    /// <summary>
    /// Gets the exit-26 failure a problem with the netrc file ends the transfer with, which only a
    /// required file does; under <c>--netrc-optional</c> curl carries on.
    /// </summary>
    /// <param name="use">Whether the file is required.</param>
    /// <param name="message">What is wrong with the file, or <see langword="null" />.</param>
    /// <returns>The failure, or <see langword="null" /> when the transfer goes ahead.</returns>
    private static TransferResult? FailureOf(NetrcUse use, string? message) =>
        message is not null && use == NetrcUse.Required ? TransferResult.Failure(CurlExitCode.ReadError, message) : null;

    /// <summary>
    /// Gets the credentials to send: a matching entry's, with the URL's user name when the entry has
    /// no login, else the URL's own. The URL's password never fills an entry's missing password:
    /// curl 8.21.0 sends <c>lo:</c> for <c>http://:up@host/</c> and an entry with only
    /// <c>login lo</c> (measured 2026-10-03, BL-1356 Notes).
    /// </summary>
    /// <param name="result">The lookup in the netrc file.</param>
    /// <param name="urlUser">The URL's decoded user name, or <see langword="null" />.</param>
    /// <param name="urlPassword">The URL's decoded password, or <see langword="null" />.</param>
    /// <returns>The credentials, or <see langword="null" /> when neither gives a user name.</returns>
    private static NetworkCredential? CredentialsOf(NetrcLookupResult result, string? urlUser, string? urlPassword) =>
        result.Outcome == NetrcLookupOutcome.Found
            ? new NetworkCredential(result.Login ?? urlUser ?? string.Empty, result.Password ?? string.Empty)
            : CredentialsOfUrl(urlUser, urlPassword);

    /// <summary>
    /// Gets the URL's own credentials, sent when the netrc file has no entry for it.
    /// </summary>
    /// <param name="urlUser">The URL's decoded user name, or <see langword="null" />.</param>
    /// <param name="urlPassword">The URL's decoded password, or <see langword="null" />.</param>
    /// <returns>The credentials, or <see langword="null" /> when the URL has no user name.</returns>
    private static NetworkCredential? CredentialsOfUrl(string? urlUser, string? urlPassword) =>
        urlUser is null ? null : new NetworkCredential(urlUser, urlPassword ?? string.Empty);

    /// <summary>
    /// Percent-decodes a user name or password from the URL; an empty one counts as none, as
    /// curl's <c>http://@host/</c> sends no user.
    /// </summary>
    /// <param name="encoded">The part as written in the URL, or <see langword="null" />.</param>
    /// <returns>The decoded part, or <see langword="null" /> when it is absent or empty.</returns>
    private static string? DecodedUserInformation(string? encoded) =>
        string.IsNullOrEmpty(encoded) ? null : Uri.UnescapeDataString(encoded);

    /// <summary>
    /// Reads the first netrc file there is, as UTF-8 with any byte order mark kept as text, as
    /// curl reads bytes.
    /// </summary>
    /// <param name="options">The option group, which may name the file.</param>
    /// <returns>The file's text, or <see langword="null" /> when none could be read.</returns>
    private string? ReadNetrcText(CommandLineOptions options)
    {
        foreach (string path in CandidatePaths(options))
        {
            if (fileReader.TryReadFile(path, out byte[] contents))
            {
                return Encoding.UTF8.GetString(contents);
            }
        }

        return null;
    }

    /// <summary>
    /// Lists where curl 8.21.0 looks for the netrc file, in order.
    /// </summary>
    /// <param name="options">The option group, which may name the file.</param>
    /// <returns>
    /// The <c>--netrc-file</c> path alone; else <c>.netrc</c> in the home directory, then on Windows
    /// <c>_netrc</c> there; else nothing when there is no home directory.
    /// </returns>
    private IEnumerable<string> CandidatePaths(CommandLineOptions options)
    {
        if (options.NetrcFile is { } file)
        {
            return [file];
        }

        if (HomeDirectory() is not { } home)
        {
            return [];
        }

        return runsOnWindows ? [home + "\\.netrc", home + "\\_netrc"] : [home + "/.netrc"];
    }

    /// <summary>
    /// Gets the home directory curl looks in: <c>HOME</c>, else on Windows <c>USERPROFILE</c>. An empty
    /// value counts as unset.
    /// </summary>
    /// <returns>The directory, or <see langword="null" /> when there is none.</returns>
    private string? HomeDirectory() =>
        NonEmpty(readEnvironmentVariable("HOME"))
        ?? (runsOnWindows ? NonEmpty(readEnvironmentVariable("USERPROFILE")) : null);

    /// <summary>Gets <paramref name="value" />, or <see langword="null" /> when it is empty.</summary>
    /// <param name="value">An environment variable's value, or <see langword="null" />.</param>
    /// <returns>The value when it has characters.</returns>
    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

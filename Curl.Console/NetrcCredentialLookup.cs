using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

using Curl.Authentication;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Chooses a transfer's credentials from the netrc file when <c>-n</c>, <c>--netrc-file</c> or
/// <c>--netrc-optional</c> asks, with curl 8.21.0's precedence against <c>-u</c> and the URL's user
/// information (measured 2026-09-28, BL-505 Notes).
/// </summary>
/// <remarks>
/// <para>
/// A <c>-u</c> with a user name wins and no file is read, so a missing or broken file is not
/// noticed; <c>-u :</c> or <c>-u :pw</c> has no user name and does not count. Otherwise the file
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
/// The lookup is made once per URL, for its host. A redirect keeps the credentials to the same
/// host and drops them to another, as <c>RedirectFollower</c> does; curl looks each hop's host up
/// again, which is BL-790.
/// </para>
/// </remarks>
/// <param name="fileReader">Reads the netrc file.</param>
/// <param name="readEnvironmentVariable">
/// Returns an environment variable's value, or <see langword="null" /> when it is not set; read
/// for <c>HOME</c> and, on Windows, <c>USERPROFILE</c>.
/// </param>
/// <param name="runsOnWindows">Whether the default location is looked for as curl's Windows build does.</param>
internal sealed class NetrcCredentialLookup(
    IDataFileReader fileReader,
    Func<string, string?> readEnvironmentVariable,
    bool runsOnWindows)
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
    /// <see langword="null" /> when the netrc file has nothing to say, so the <c>-u</c> ones stand.
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
        if (!ReadsNetrc(options))
        {
            return true;
        }

        string? urlUser = DecodedUserInformation(url.User);
        string? text = ReadNetrcText(options);
        NetrcLookupResult result = text is null ? NetrcLookupResult.NotFound : NetrcFile.Find(text, url.Host, urlUser);
        failure = FailureOf(options.NetrcUse, FailureMessageOf(text, result));
        credentials = CredentialsOf(result, urlUser, DecodedUserInformation(url.Password));
        return failure is null;
    }

    /// <summary>
    /// Tells whether the transfer reads the netrc file: one of the netrc options is in effect and
    /// <c>-u</c> gave no user name.
    /// </summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <returns><see langword="true" /> when the file is read.</returns>
    private static bool ReadsNetrc(CommandLineOptions options) =>
        options.NetrcUse != NetrcUse.Ignored && string.IsNullOrEmpty(options.Credentials?.UserName);

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
    /// Gets the credentials to send: a matching entry's merged with the URL's, else the URL's own.
    /// </summary>
    /// <param name="result">The lookup in the netrc file.</param>
    /// <param name="urlUser">The URL's decoded user name, or <see langword="null" />.</param>
    /// <param name="urlPassword">The URL's decoded password, or <see langword="null" />.</param>
    /// <returns>The credentials, or <see langword="null" /> when neither gives a user name.</returns>
    private static NetworkCredential? CredentialsOf(NetrcLookupResult result, string? urlUser, string? urlPassword) =>
        result.Outcome == NetrcLookupOutcome.Found
            ? new NetworkCredential(result.Login ?? urlUser ?? string.Empty, result.Password ?? urlPassword ?? string.Empty)
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

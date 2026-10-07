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
/// <c>(26) .netrc error: syntax error</c> on the same terms. The runner writes every failure this
/// lookup returns as a <c>-v</c> info line first, as curl 8.21.0's <c>failf</c> does (BL-1411, BL-1447).
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
/// <para>
/// Credentials holding a control character are refused before anything is sent, as curl 8.21.0's
/// <c>lib/url.c</c> refuses them (BL-1411): URL ones that decode to a byte below 0x20 (only 0x00 for
/// <c>http</c>, <c>https</c>, <c>ws</c> and <c>wss</c>) with exit 3 and
/// <see cref="UrlCredentialsMessage" />, on every redirect hop too
/// (<see cref="TryCheckUrlCredentials" />); a matching netrc entry's with exit 26 and
/// <see cref="NetrcControlCodeMessage" />, except over those four schemes.
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
    /// The message curl 8.21.0 prints after <c>curl: (3) </c> when the URL's user name or password
    /// percent-decodes to a control character its scheme refuses (<c>lib/url.c</c>, BL-1411).
    /// </summary>
    internal const string UrlCredentialsMessage = "error extracting credentials from URL";

    /// <summary>
    /// The message curl 8.21.0 prints after <c>curl: (26) </c> when a netrc entry's login or
    /// password holds a control character its scheme refuses (<c>lib/url.c</c>, BL-1411).
    /// </summary>
    internal const string NetrcControlCodeMessage = "control code detected in .netrc credentials";

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
    /// <param name="failure">
    /// The exit-3 failure for URL credentials holding a control character, or the exit-26 one for
    /// the netrc file, the transfer ends with, when it fails.
    /// </param>
    /// <param name="reportInfo">
    /// Gets curl 8.21.0's <c>-v</c> line <see cref="HostNotFoundLine" /> when the file was looked in
    /// and gave the host nothing; <see langword="null" /> writes nothing, as for a redirect hop.
    /// </param>
    /// <returns><see langword="false" /> when the transfer fails before it starts.</returns>
    internal bool TryLookUp(
        CommandLineOptions options,
        CurlUrl url,
        out NetworkCredential? credentials,
        [NotNullWhen(false)] out TransferResult? failure,
        Action<string>? reportInfo = null)
    {
        credentials = null;
        if (!TryCheckUrlCredentials(options, url, out failure) || UserOptionWins(options))
        {
            return failure is null;
        }

        if (options.NetrcUse == NetrcUse.Ignored)
        {
            credentials = CredentialsWrittenInUrl(url);
            return true;
        }

        return TryLookUpInNetrcFile(options, url, out credentials, out failure, reportInfo);
    }

    /// <summary>
    /// Looks <paramref name="url" />'s host up in the netrc file, for <see cref="TryLookUp" />, once
    /// neither <c>-u</c> nor the absence of a netrc option has decided the credentials.
    /// </summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="credentials">The credentials to send, as <see cref="TryLookUp" /> describes them.</param>
    /// <param name="failure">The exit-26 failure the netrc file ends the transfer with, when it does.</param>
    /// <param name="reportInfo">Gets <see cref="HostNotFoundLine" /> when the file gave the host nothing, or <see langword="null" />.</param>
    /// <returns><see langword="false" /> when the transfer fails before it starts.</returns>
    private bool TryLookUpInNetrcFile(
        CommandLineOptions options,
        CurlUrl url,
        out NetworkCredential? credentials,
        [NotNullWhen(false)] out TransferResult? failure,
        Action<string>? reportInfo)
    {
        string? urlUser = DecodedUserInformation(url.User);
        string? text = ReadNetrcText(options);
        NetrcLookupResult result = text is null ? NetrcLookupResult.NotFound : NetrcFile.Find(text, url.Host, urlUser, diagnosticLog);
        failure = FailureOf(options.NetrcUse, FailureMessageOf(text, result)) ?? NetrcControlCodeFailure(url, result);
        if (failure is null && result.Outcome != NetrcLookupOutcome.Found)
        {
            reportInfo?.Invoke(HostNotFoundLine(url.Host, options.NetrcFile));
        }

        credentials = CredentialsOf(result, urlUser, DecodedUserInformation(url.Password));
        return failure is null;
    }

    /// <summary>
    /// Refuses <paramref name="url" /> when the credentials written in it percent-decode to a control
    /// character its scheme refuses, as curl 8.21.0 decodes them with <c>REJECT_CTRL</c> (any byte
    /// below 0x20), or <c>REJECT_ZERO</c> (only 0x00) for <c>http</c>, <c>https</c>, <c>ws</c> and
    /// <c>wss</c>, before it connects (measured 2026-10-03, BL-1411). A <c>-u</c> with a user name
    /// wins over the URL's credentials, so they are not checked under one. Each redirect hop's URL is
    /// checked the same way.
    /// </summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <param name="url">The transfer's or the redirect hop's URL.</param>
    /// <param name="failure">The exit-3 failure, when the credentials are refused.</param>
    /// <returns><see langword="false" /> when the credentials are refused.</returns>
    internal static bool TryCheckUrlCredentials(
        CommandLineOptions options,
        CurlUrl url,
        [NotNullWhen(false)] out TransferResult? failure)
    {
        failure = !UserOptionWins(options) && (HasRefusedControlCode(url, DecodedUserInformation(url.User)) || HasRefusedControlCode(url, DecodedUserInformation(url.Password)))
            ? TransferResult.Failure(CurlExitCode.UrlMalformat, UrlCredentialsMessage)
            : null;
        return failure is null;
    }

    /// <summary>
    /// curl 8.21.0's <c>-v</c> line for a netrc file that gave the host no entry, or, under
    /// <c>--netrc-optional</c>, could not be read or parsed (<c>lib/url.c</c>'s <c>override_login</c>,
    /// measured 2026-10-04, BL-1432 Notes): the host as the URL spells it, and the
    /// <c>--netrc-file</c> path as given or the literal <c>.netrc</c>.
    /// </summary>
    /// <param name="host">The URL's host.</param>
    /// <param name="netrcFile">The <c>--netrc-file</c> path, or <see langword="null" />.</param>
    /// <returns>The line, without its <c>* </c> prefix.</returns>
    internal static string HostNotFoundLine(string host, string? netrcFile) =>
        $"Could not find host {host} in the {netrcFile ?? ".netrc"} file; using defaults";

    /// <summary>Whether <c>-u</c> gives a user name, which wins over the URL's and the netrc file's credentials.</summary>
    /// <param name="options">The option group of the transfer.</param>
    /// <returns><see langword="true" /> when <c>-u</c> gives a user name.</returns>
    private static bool UserOptionWins(CommandLineOptions options) =>
        !string.IsNullOrEmpty(options.Credentials?.UserName);

    /// <summary>
    /// curl's exit-26 failure for a matching netrc entry whose login or password holds a byte below
    /// 0x20, which every scheme but <c>http</c>, <c>https</c>, <c>ws</c> and <c>wss</c> refuses before
    /// anything is sent, <c>--netrc-optional</c> or not (<c>lib/url.c</c>'s <c>str_has_ctrl</c>, BL-1411).
    /// </summary>
    /// <param name="url">The transfer's URL, whose scheme decides.</param>
    /// <param name="result">The lookup in the netrc file.</param>
    /// <returns>The failure, or <see langword="null" /> when the credentials may be sent.</returns>
    private static TransferResult? NetrcControlCodeFailure(CurlUrl url, NetrcLookupResult result) =>
        result.Outcome == NetrcLookupOutcome.Found
            && !AllowsControlCodesInCredentials(url)
            && (HasControlCode(result.Login) || HasControlCode(result.Password))
            ? TransferResult.Failure(CurlExitCode.ReadError, NetrcControlCodeMessage)
            : null;

    /// <summary>
    /// Whether the decoded URL user name or password holds a control character the URL's scheme
    /// refuses: 0x00 for <c>http</c>, <c>https</c>, <c>ws</c> and <c>wss</c>, any byte below 0x20 for
    /// the rest. 0x7f and every byte from 0x80 are accepted, as curl's <c>Curl_urldecode</c> accepts them.
    /// </summary>
    /// <param name="url">The URL, whose scheme decides.</param>
    /// <param name="decoded">The decoded part, or <see langword="null" /> when absent.</param>
    /// <returns><see langword="true" /> when the part is refused.</returns>
    private static bool HasRefusedControlCode(CurlUrl url, string? decoded) =>
        AllowsControlCodesInCredentials(url) ? decoded?.Contains('\0') == true : HasControlCode(decoded);

    /// <summary>Whether <paramref name="text" /> holds a character below 0x20.</summary>
    /// <param name="text">A user name or password, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when it holds a control character.</returns>
    private static bool HasControlCode(string? text) =>
        text is not null && text.AsSpan().IndexOfAnyInRange('\0', '\u001f') >= 0;

    /// <summary>
    /// Whether the URL's scheme is one of the four that carry curl's <c>PROTOPT_USERPWDCTRL</c>
    /// (<c>lib/protocol.c</c>): <c>http</c>, <c>https</c>, <c>ws</c> and <c>wss</c>.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true" /> for those four schemes.</returns>
    private static bool AllowsControlCodesInCredentials(CurlUrl url) =>
        url.Scheme is "http" or "https" or "ws" or "wss";

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

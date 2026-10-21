using System.Collections.Frozen;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Finds the login and password for a host in netrc text, picking the entry curl 8.21.0's
/// netrc parser picks (BL-503).
/// </summary>
/// <remarks>
/// <para>
/// The file is a run of whitespace-separated tokens. <c>machine</c> <em>name</em> starts an
/// entry for the host <em>name</em>, compared without regard to case; <c>default</c> starts
/// an entry for every host; <c>login</c> and <c>password</c> each take the next token as
/// their value, the last one given winning; <c>macdef</c> ends the entry and skips a macro
/// that runs to the first blank line. Keywords are matched without regard to case, any other
/// token is ignored, and a token starting with <c>#</c> where a keyword is expected comments
/// out the rest of its line. A value is taken verbatim, even when it looks like a keyword
/// or starts with <c>#</c>.
/// </para>
/// <para>
/// Entries are tried in file order and the first that qualifies wins, even a
/// <c>default</c> ahead of a <c>machine</c> for the host. Without a user name, an entry
/// qualifies when it has a login or a password. With one, it qualifies when it has a
/// password and its login, if any, equals the user name case-sensitively. Reading stops at
/// the end of the winning entry, so a syntax error after it is never seen.
/// </para>
/// </remarks>
public static class NetrcFile
{
    /// <summary>
    /// The <c>dynbuf</c> limit curl 8.22.0's netrc parser reads each line into with <c>Curl_get_line</c>:
    /// a line of this many bytes or more, its line feed included (one is added to a last line without
    /// one), fails the whole file as a syntax error, wherever in the file it is (measured 2026-10-21, BL-2916).
    /// </summary>
    public const int LineBufferLimit = 16384;

    /// <summary>
    /// Whether this platform's curl opens the netrc file in text mode (<c>FOPEN_READTEXT</c>, <c>"rt"</c>):
    /// on Windows, where the C runtime then ends the file at its first <c>^Z</c> and reads CR LF as LF.
    /// </summary>
    private static readonly bool PlatformReadsInTextMode = OperatingSystem.IsWindows();

    /// <summary>Looks the host up in netrc text, read as this platform's curl reads the file.</summary>
    /// <param name="text">The netrc file's text.</param>
    /// <param name="hostName">The host name of the URL.</param>
    /// <param name="userName">
    /// The user name of the URL, which selects among entries for the host;
    /// <see langword="null" /> when the URL has none.
    /// </param>
    /// <param name="diagnosticLog">
    /// Where a matching entry is logged at <c>info</c>, by host and login only, never its password
    /// (BL-1151); <see langword="null" /> logs nothing.
    /// </param>
    /// <returns>The entry curl picks, no entry, or a syntax error.</returns>
    public static NetrcLookupResult Find(string text, string hostName, string? userName, IDiagnosticLog? diagnosticLog = null) =>
        FindWithReadMode(text, hostName, userName, PlatformReadsInTextMode, diagnosticLog);

    /// <summary>
    /// Looks the host up in netrc text as <see cref="Find(string, string, string?, IDiagnosticLog?)" /> does,
    /// reading it in text mode (as curl on Windows does) or in binary mode (as curl on Linux and macOS does).
    /// </summary>
    /// <remarks>
    /// In text mode the text ends at its first <c>^Z</c> (0x1A) and each CR LF is one line feed, as the
    /// Windows C runtime reads a file opened <c>"rt"</c> (measured 2026-10-21 on curl 8.22.0 for Windows,
    /// BL-2916). In binary mode every character is kept. Then any line whose UTF-8 bytes and line feed
    /// reach <see cref="LineBufferLimit" /> makes the file a syntax error.
    /// </remarks>
    /// <param name="text">The netrc file's text.</param>
    /// <param name="hostName">The host name of the URL.</param>
    /// <param name="userName">The user name of the URL; <see langword="null" /> when the URL has none.</param>
    /// <param name="readsInTextMode"><see langword="true" /> to read the text as a file opened in Windows text mode.</param>
    /// <param name="diagnosticLog">Where a matching entry is logged; <see langword="null" /> logs nothing.</param>
    /// <returns>The entry curl picks, no entry, or a syntax error.</returns>
    internal static NetrcLookupResult FindWithReadMode(string text, string hostName, string? userName, bool readsInTextMode, IDiagnosticLog? diagnosticLog = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(hostName);
        string fileText = readsInTextMode ? AsReadInTextMode(text) : text;
        NetrcLookupResult result = HasLineTooLong(fileText)
            ? NetrcLookupResult.SyntaxError
            : new NetrcLookup(new NetrcTokenScanner(fileText), hostName, userName).Run();
        if (result.Outcome == NetrcLookupOutcome.Found)
        {
            new AuthDiagnosticLog(diagnosticLog).NetrcEntryMatched(hostName, result.Login);
        }

        return result;
    }

    /// <summary>
    /// Looks the host up in a netrc file read from a stream as UTF-8, a byte order mark
    /// included as text as curl includes it.
    /// </summary>
    /// <param name="stream">The netrc file's contents.</param>
    /// <param name="hostName">The host name of the URL.</param>
    /// <param name="userName">
    /// The user name of the URL; <see langword="null" /> when the URL has none.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry curl picks, no entry, or a syntax error.</returns>
    public static async Task<NetrcLookupResult> FindAsync(
        Stream stream,
        string hostName,
        string? userName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Find(text, hostName, userName);
    }

    private static string AsReadInTextMode(string text)
    {
        int controlZ = text.IndexOf('\u001A', StringComparison.Ordinal);
        string beforeControlZ = controlZ < 0 ? text : text[..controlZ];
        return beforeControlZ.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static bool HasLineTooLong(string text) =>
        text.Split('\n').Any(line => Encoding.UTF8.GetByteCount(line) + 1 >= LineBufferLimit);

    private sealed class NetrcLookup(NetrcTokenScanner scanner, string hostName, string? userName)
    {
        private bool inMatchingEntry;

        private string? login;

        private string? password;

        internal NetrcLookupResult Run()
        {
            for (string? keyword = scanner.ReadKeyword(); keyword is not null; keyword = scanner.ReadKeyword())
            {
                if (ApplyKeyword(keyword) is { } result)
                {
                    return result;
                }
            }

            return scanner.HasSyntaxError ? NetrcLookupResult.SyntaxError : EndEntry() ?? NetrcLookupResult.NotFound;
        }

        private NetrcLookupResult? ApplyKeyword(string keyword) =>
            KeywordActions.TryGetValue(keyword, out Func<NetrcLookup, NetrcLookupResult?>? action) ? action(this) : null;

        private static readonly FrozenDictionary<string, Func<NetrcLookup, NetrcLookupResult?>> KeywordActions =
            new Dictionary<string, Func<NetrcLookup, NetrcLookupResult?>>
            {
                ["machine"] = lookup => lookup.EndEntry() ?? lookup.StartMachineEntry(),
                ["default"] = lookup => lookup.EndEntry() ?? lookup.StartEntry(matchesHost: true),
                ["macdef"] = lookup => lookup.EndEntry() ?? lookup.SkipMacroDefinition(),
                ["login"] = lookup => lookup.ReadValue(ref lookup.login),
                ["password"] = lookup => lookup.ReadValue(ref lookup.password),
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private NetrcLookupResult? StartMachineEntry() =>
            StartEntry(string.Equals(scanner.ReadToken(), hostName, StringComparison.OrdinalIgnoreCase));

        private NetrcLookupResult? StartEntry(bool matchesHost)
        {
            inMatchingEntry = matchesHost;
            login = null;
            password = null;
            return null;
        }

        private NetrcLookupResult? SkipMacroDefinition()
        {
            scanner.SkipMacroDefinition();
            return null;
        }

        private NetrcLookupResult? ReadValue(ref string? value)
        {
            // A keyword left without a value at the end of the text keeps the earlier one.
            value = scanner.ReadToken() ?? value;
            return null;
        }

        private NetrcLookupResult? EndEntry()
        {
            bool qualifies = inMatchingEntry && (userName is null ? HasLoginOrPassword() : ServesUser(userName));
            inMatchingEntry = false;
            return qualifies ? NetrcLookupResult.Found(login, password) : null;
        }

        private bool HasLoginOrPassword() => login is not null || password is not null;

        private bool ServesUser(string user) =>
            password is not null && (login is null || string.Equals(login, user, StringComparison.Ordinal));
    }
}

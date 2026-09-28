using System.Collections.Frozen;
using System.Text;

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
    /// <summary>Looks the host up in netrc text.</summary>
    /// <param name="text">The netrc file's text.</param>
    /// <param name="hostName">The host name of the URL.</param>
    /// <param name="userName">
    /// The user name of the URL, which selects among entries for the host;
    /// <see langword="null" /> when the URL has none.
    /// </param>
    /// <returns>The entry curl picks, no entry, or a syntax error.</returns>
    public static NetrcLookupResult Find(string text, string hostName, string? userName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(hostName);
        return new NetrcLookup(new NetrcTokenScanner(text), hostName, userName).Run();
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
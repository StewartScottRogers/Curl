namespace Curl.Protocol.Abstractions;

/// <summary>
/// What curl 8.21.0 knows about a URL scheme: how it finds one in the text, the one it
/// guesses when there is none, each scheme's default port, and which schemes take login
/// options.
/// </summary>
internal static class CurlUrlScheme
{
    /// <summary>The length at which curl stops reading a scheme (<c>MAX_SCHEME_LEN</c>).</summary>
    private const int MaximumLength = 40;

    private static readonly Dictionary<string, int> DefaultPorts = new(StringComparer.Ordinal)
    {
        ["dict"] = 2628,
        ["ftp"] = 21,
        ["ftps"] = 990,
        ["gopher"] = 70,
        ["gophers"] = 70,
        ["http"] = 80,
        ["https"] = 443,
        ["imap"] = 143,
        ["imaps"] = 993,
        ["ldap"] = 389,
        ["ldaps"] = 636,
        ["mqtt"] = 1883,
        ["mqtts"] = 8883,
        ["pop3"] = 110,
        ["pop3s"] = 995,
        ["rtsp"] = 554,
        ["scp"] = 22,
        ["sftp"] = 22,
        ["smb"] = 445,
        ["smbs"] = 445,
        ["smtp"] = 25,
        ["smtps"] = 465,
        ["telnet"] = 23,
        ["tftp"] = 69,
        ["ws"] = 80,
        ["wss"] = 443,
    };

    private static readonly HashSet<string> SchemesWithLoginOptions = new(StringComparer.Ordinal)
    {
        "imap", "imaps", "pop3", "pop3s", "smtp", "smtps",
    };

    private static readonly string[] GuessedPrefixes = ["ftp", "dict", "ldap", "imap", "smtp", "pop3"];

    /// <summary>
    /// Reads the scheme at the start of <paramref name="text" /> as curl's
    /// <c>Curl_is_absolute_url</c> does when it guesses schemes: a letter, then letters,
    /// digits, <c>+</c>, <c>-</c> or <c>.</c>, then <c>:/</c>.
    /// </summary>
    /// <returns>The scheme in lower case, or <see langword="null" /> when the text has none.</returns>
    public static string? Read(string text, bool driveLetters)
    {
        if (!StartsLikeAScheme(text, driveLetters))
        {
            return null;
        }

        int length = SchemeLength(text);
        bool endsWithColonSlash = text.AsSpan(length).StartsWith(":/", StringComparison.Ordinal);

        return endsWithColonSlash ? text[..length].ToLowerInvariant() : null;
    }

    /// <summary>
    /// Guesses the scheme of a URL typed without one from its host, as curl's
    /// <c>guess_scheme</c> does: <c>ftp.</c>, <c>dict.</c>, <c>ldap.</c>, <c>imap.</c>,
    /// <c>smtp.</c> and <c>pop3.</c> name their scheme, and anything else is <c>http</c>.
    /// </summary>
    public static string Guess(string host)
    {
        foreach (string prefix in GuessedPrefixes)
        {
            if (host.Length > prefix.Length
                && host[prefix.Length] == '.'
                && host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return prefix;
            }
        }

        return "http";
    }

    /// <summary>Gets the scheme's default port, or <see langword="null" /> when it has none.</summary>
    public static int? DefaultPort(string scheme) =>
        DefaultPorts.TryGetValue(scheme, out int port) ? port : null;

    /// <summary>
    /// Gets a value indicating whether the scheme reads login options after a <c>;</c>
    /// (curl's <c>PROTOPT_URLOPTIONS</c>).
    /// </summary>
    public static bool HasLoginOptions(string? scheme) =>
        scheme is not null && SchemesWithLoginOptions.Contains(scheme);

    /// <summary>
    /// A scheme starts with a letter; on Windows a letter and a colon is a drive, not a
    /// scheme (curl's <c>STARTS_WITH_DRIVE_PREFIX</c>).
    /// </summary>
    private static bool StartsLikeAScheme(string text, bool driveLetters) =>
        text.Length >= 2 && char.IsAsciiLetter(text[0]) && !(driveLetters && text[1] == ':');

    private static int SchemeLength(string text)
    {
        int length = 1;
        while (length < MaximumLength && length < text.Length && IsSchemeCharacter(text[length]))
        {
            length++;
        }

        return length;
    }

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.';
}

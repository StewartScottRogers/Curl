using System.Collections.Frozen;

namespace Curl.Cli;

/// <summary>
/// Reads the value of <c>--proto</c> and <c>--proto-redir</c> into the set of schemes it allows, and the
/// value of <c>--proto-default</c> into one scheme, with curl 8.21.0's syntax and warnings.
/// </summary>
/// <remarks>
/// <para>
/// A set value is a comma-separated list read left to right, starting from every scheme in
/// <see cref="KnownSchemes"/>, whatever an earlier <c>--proto</c> left. Empty items are skipped. An item may
/// start with one modifier: <c>+</c> adds the scheme (the same as none), <c>-</c> removes it, <c>=</c> makes it
/// the only one. The name after the modifier is matched case-insensitively, and <c>all</c> stands for every
/// known scheme. A name curl does not know is warned about with
/// <see cref="CommandLineWarning.UnrecognizedProtocol(string)"/>, showing at most its first 31 characters, and
/// changes nothing unless its modifier is <c>=</c>, which still empties the set. A value that leaves the set
/// empty is refused as badly used.
/// </para>
/// <para>
/// Measured with <c>Record-CurlExchange.ps1</c> against the Windows curl 8.21.0 on 2026-09-28 (BL-522):
/// <c>=http,https</c>, <c>-all,+http</c>, <c>HTTP</c>, <c>=HtTp</c> and an empty value are taken silently;
/// <c>http,bogus</c> warns once; <c>++http</c> warns about <c>+http</c>; <c>=,http</c> and <c>+</c> warn about
/// <c>''</c>; <c>' http , bogus'</c> warns about <c>' http '</c> and <c>' bogus'</c>; <c>-all</c>,
/// <c>=http,-http</c>, <c>=bogus</c> and <c>=</c> are refused, the last two after their warning; <c>ipfs</c>,
/// <c>rtmp</c> and <c>smb</c> are unknown to that build.
/// </para>
/// </remarks>
internal static class CommandLineProtocolSet
{
    /// <summary>The longest name curl 8.21.0 repeats in its unrecognized-protocol warning.</summary>
    private const int LongestWarnedName = 31;

    /// <summary>
    /// The schemes curl 8.21.0 knows for these options: the <c>Protocols:</c> line of the Windows (Schannel)
    /// build's <c>curl -V</c>, less <c>ipfs</c> and <c>ipns</c>, which that build's curl tool handles itself and
    /// its libcurl does not know. The same set on every platform, so a command line reads the same everywhere
    /// (ADR-0189).
    /// </summary>
    internal static FrozenSet<string> KnownSchemes { get; } = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
        "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smtp", "smtps", "telnet", "tftp", "ws", "wss");

    /// <summary>
    /// Reads a <c>--proto</c> or <c>--proto-redir</c> value into the schemes it allows.
    /// </summary>
    /// <param name="value">The value exactly as given.</param>
    /// <param name="warningLines">Where each unrecognized-protocol warning is appended, in the order met.</param>
    /// <returns>The allowed schemes, lowercase; <see langword="null"/> when the value leaves none.</returns>
    internal static FrozenSet<string>? Read(string value, List<string> warningLines)
    {
        HashSet<string> allowed = new(KnownSchemes, StringComparer.Ordinal);
        foreach (string item in value.Split(','))
        {
            if (item.Length > 0)
            {
                ApplyItem(allowed, item, warningLines);
            }
        }

        return allowed.Count == 0 ? null : allowed.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The lowercase name of a known scheme, for <c>--proto-default</c>; <see langword="null"/> for a name
    /// curl 8.21.0 does not know, <c>all</c> included, which it refuses as unsupported.
    /// </summary>
    /// <param name="name">The value exactly as given.</param>
    /// <returns>The scheme, lowercase, or <see langword="null"/>.</returns>
    internal static string? KnownScheme(string name) =>
        KnownSchemes.TryGetValue(name, out string? scheme) ? scheme : null;

    private static void ApplyItem(HashSet<string> allowed, string item, List<string> warningLines)
    {
        char modifier = SplitModifier(item, out string name);
        if (string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
        {
            ApplyToAll(allowed, modifier);
        }
        else
        {
            ApplyToName(allowed, modifier, name, warningLines);
        }
    }

    /// <summary>
    /// The item's one leading <c>+</c>, <c>-</c> or <c>=</c>, or <c>+</c> when it has none, with
    /// <paramref name="name"/> set to the rest.
    /// </summary>
    private static char SplitModifier(string item, out string name)
    {
        if (item[0] is '+' or '-' or '=')
        {
            name = item[1..];
            return item[0];
        }

        name = item;
        return '+';
    }

    private static void ApplyToName(HashSet<string> allowed, char modifier, string name, List<string> warningLines)
    {
        string? scheme = KnownScheme(name);
        if (modifier == '=')
        {
            allowed.Clear();
        }

        if (scheme is null)
        {
            warningLines.Add(CommandLineWarning.UnrecognizedProtocol(name[..Math.Min(name.Length, LongestWarnedName)]));
        }
        else if (modifier == '-')
        {
            allowed.Remove(scheme);
        }
        else
        {
            allowed.Add(scheme);
        }
    }

    private static void ApplyToAll(HashSet<string> allowed, char modifier)
    {
        if (modifier == '-')
        {
            allowed.Clear();
        }
        else
        {
            allowed.UnionWith(KnownSchemes);
        }
    }
}

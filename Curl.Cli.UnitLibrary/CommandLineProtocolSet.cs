using System.Collections.Frozen;

namespace Curl.Cli;

/// <summary>
/// Reads the value of <c>--proto</c> and <c>--proto-redir</c> into the set of schemes it allows, and the
/// value of <c>--proto-default</c> into one scheme, with curl 8.22.0's syntax and refusals.
/// </summary>
/// <remarks>
/// <para>
/// A set value is a comma-separated list read left to right, starting from every scheme in
/// <see cref="KnownSchemes"/>, whatever an earlier <c>--proto</c> left. Empty items are skipped. An item may
/// start with one modifier: <c>+</c> adds the scheme (the same as none), <c>-</c> removes it, <c>=</c> makes it
/// the only one. The name after the modifier is matched case-insensitively, and <c>all</c> stands for every
/// known scheme. The first name curl does not know ends the reading: the caller refuses the option with
/// <see cref="CommandLineRefusal.UnrecognizedProtocol(string, string, bool)"/>, repeating at most the name's
/// first 31 characters. A value that leaves the set empty is refused as badly used.
/// </para>
/// <para>
/// Measured against the Windows curl 8.22.0 on 2026-10-13 (BL-2143, AF-0184): <c>x</c>, <c>http,bogus</c>,
/// <c>=bogus</c>, <c>-bogus</c>, <c>x,-all</c>, <c>ipfs</c>, <c>rtmp</c> and <c>smb</c> print
/// <c>curl: unrecognized protocol '&lt;name&gt;'</c>, then <c>is badly used here</c> and the try-help line, and
/// exit 2 sending nothing, for <c>--proto-redir</c> too; <c>++http</c> repeats <c>+http</c>, <c>+</c> and
/// <c>=,http</c> repeat <c>''</c>, and a long name is cut to 31 characters; <c>-s</c> drops the first line and
/// <c>-sS</c> keeps it; <c>HTTP</c> is taken. curl 8.21.0 only warned and went on (BL-522).
/// </para>
/// <para>
/// <c>smb</c> and <c>smbs</c> are unrecognized here on every platform, as neither reference build of curl
/// 8.22.0 has SMB: measured on Windows on 2026-10-26, <c>--proto =smb -sS smb://127.0.0.1:20002/share/x</c>
/// exits 2 with <c>curl: unrecognized protocol 'smb'</c> and sends nothing (CGR-043, BL-3888, ADR-0835).
/// <see cref="KnownScheme"/>, which <c>--proto-default</c> reads, still knows them.
/// </para>
/// </remarks>
internal static class CommandLineProtocolSet
{
    /// <summary>The longest name curl 8.22.0 repeats in its unrecognized-protocol line.</summary>
    private const int LongestRepeatedName = 31;

    /// <summary>
    /// The schemes curl 8.21.0 knows for these options: the <c>Protocols:</c> line of the Windows (Schannel)
    /// build's <c>curl -V</c>, less <c>ipfs</c> and <c>ipns</c>, which that build's curl tool handles itself and
    /// its libcurl does not know, plus <c>smb</c> and <c>smbs</c>, which Curl serves and the Linux (OpenSSL) build
    /// knows. The same set on every platform, so a command line reads the same everywhere
    /// (ADR-0189).
    /// </summary>
    internal static FrozenSet<string> KnownSchemes { get; } = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
        "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smb", "smbs", "smtp", "smtps", "telnet", "tftp", "ws",
        "wss");

    /// <summary>
    /// The schemes <c>--proto</c> and <c>--proto-redir</c> know: <see cref="KnownSchemes"/> less <c>smb</c> and
    /// <c>smbs</c>, which curl 8.22.0 refuses there as unrecognized (BL-3888).
    /// </summary>
    private static FrozenSet<string> ProtoSchemes { get; } =
        KnownSchemes.Except(["smb", "smbs"], StringComparer.OrdinalIgnoreCase).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a <c>--proto</c> or <c>--proto-redir</c> value into the schemes it allows, stopping at the first
    /// name curl does not know.
    /// </summary>
    /// <param name="value">The value exactly as given.</param>
    /// <param name="unrecognizedName">
    /// The first name curl does not know, without its modifier and cut to its first 31 characters, as curl
    /// repeats it; <see langword="null"/> when every name is known.
    /// </param>
    /// <returns>
    /// The allowed schemes, lowercase; <see langword="null"/> when a name is unrecognized or the value leaves none.
    /// </returns>
    internal static FrozenSet<string>? Read(string value, out string? unrecognizedName)
    {
        HashSet<string> allowed = new(ProtoSchemes, StringComparer.Ordinal);
        unrecognizedName = null;
        foreach (string item in value.Split(','))
        {
            unrecognizedName = item.Length == 0 ? null : ApplyItem(allowed, item);
            if (unrecognizedName is not null)
            {
                return null;
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

    /// <summary>
    /// Applies one non-empty item to <paramref name="allowed"/>, or returns its name, as curl repeats it, when
    /// curl does not know it.
    /// </summary>
    private static string? ApplyItem(HashSet<string> allowed, string item)
    {
        char modifier = SplitModifier(item, out string name);
        if (string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
        {
            ApplyToAll(allowed, modifier);
            return null;
        }

        if (!ProtoSchemes.TryGetValue(name, out string? scheme))
        {
            return name[..Math.Min(name.Length, LongestRepeatedName)];
        }

        ApplyToScheme(allowed, modifier, scheme);
        return null;
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

    private static void ApplyToScheme(HashSet<string> allowed, char modifier, string scheme)
    {
        if (modifier == '-')
        {
            allowed.Remove(scheme);
            return;
        }

        if (modifier == '=')
        {
            allowed.Clear();
        }

        allowed.Add(scheme);
    }

    private static void ApplyToAll(HashSet<string> allowed, char modifier)
    {
        if (modifier == '-')
        {
            allowed.Clear();
        }
        else
        {
            allowed.UnionWith(ProtoSchemes);
        }
    }
}

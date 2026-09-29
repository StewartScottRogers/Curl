namespace Curl.Kerberos;

/// <summary>
/// A parsed <c>krb5.conf</c>, with the relations the hand-built client reads, each with
/// MIT's default when the file does not set it (ADR-0160).
/// </summary>
/// <param name="root">The parsed tree; its children are the file's sections.</param>
public sealed class KerberosConfiguration(KerberosConfigurationNode root)
{
    /// <summary>MIT's <c>udp_preference_limit</c> when the file does not set a usable one.</summary>
    public const int DefaultUdpPreferenceLimit = 1465;

    /// <summary>The largest <c>udp_preference_limit</c> MIT honours; a larger one is lowered to it.</summary>
    public const int MaximumUdpPreferenceLimit = 32700;

    /// <summary>MIT's <c>permitted_enctypes</c> when the file does not set it: the keyword for its default list.</summary>
    public const string DefaultEncryptionTypes = "DEFAULT";

    private const string LibraryDefaults = "libdefaults";

    private const string HexadecimalDigits = "0123456789abcdef";

    private static readonly string[] TrueWords = ["y", "yes", "true", "t", "1", "on"];

    private static readonly string[] FalseWords = ["n", "no", "false", "nil", "0", "off"];

    /// <summary>Gets an empty configuration, what MIT uses when no <c>krb5.conf</c> exists.</summary>
    public static KerberosConfiguration Empty => new(new KerberosConfigurationNode(string.Empty, null));

    /// <summary>Gets the parsed tree; its children are the file's sections.</summary>
    public KerberosConfigurationNode Root { get; } = root;

    /// <summary>Gets <c>[libdefaults] default_realm</c>, or <see langword="null" /> when not set.</summary>
    public string? DefaultRealm => FirstValue(LibraryDefaults, "default_realm");

    /// <summary>
    /// Gets whether KDCs may be looked up in DNS: <c>[libdefaults] dns_lookup_kdc</c>, else
    /// <c>dns_fallback</c>, else <see langword="true" />, as MIT reads them.
    /// </summary>
    public bool DnsLookupKdc =>
        ParseBoolean(FirstValue(LibraryDefaults, "dns_lookup_kdc"))
        ?? ParseBoolean(FirstValue(LibraryDefaults, "dns_fallback"))
        ?? true;

    /// <summary>
    /// Gets <c>[libdefaults] udp_preference_limit</c>, the largest message sent over UDP:
    /// <see cref="DefaultUdpPreferenceLimit" /> when unset, unparsable or negative, and at
    /// most <see cref="MaximumUdpPreferenceLimit" />.
    /// </summary>
    public int UdpPreferenceLimit
    {
        get
        {
            long? limit = ParseInteger(FirstValue(LibraryDefaults, "udp_preference_limit"));
            return limit switch
            {
                null or < 0 => DefaultUdpPreferenceLimit,
                > MaximumUdpPreferenceLimit => MaximumUdpPreferenceLimit,
                _ => (int)limit.Value,
            };
        }
    }

    /// <summary>
    /// Gets the encryption type names of <c>[libdefaults] permitted_enctypes</c>, or
    /// <see cref="DefaultEncryptionTypes" /> when not set, split on whitespace and commas.
    /// Keywords such as <c>DEFAULT</c> and <c>-des</c> are kept as written.
    /// </summary>
    public IReadOnlyList<string> PermittedEncryptionTypes =>
        SplitEncryptionTypes(FirstValue(LibraryDefaults, "permitted_enctypes") ?? DefaultEncryptionTypes);

    /// <summary>
    /// Gets the encryption type names of <c>[libdefaults] default_tkt_enctypes</c>, or
    /// <see cref="PermittedEncryptionTypes" /> when not set, as MIT 1.18 and later default it.
    /// </summary>
    public IReadOnlyList<string> DefaultTicketEncryptionTypes
    {
        get
        {
            string? written = FirstValue(LibraryDefaults, "default_tkt_enctypes");
            return written is null ? PermittedEncryptionTypes : SplitEncryptionTypes(written);
        }
    }

    /// <summary>
    /// Gets the encryption type names of <c>[libdefaults] default_tgs_enctypes</c>, or
    /// <see cref="PermittedEncryptionTypes" /> when not set, as MIT 1.18 and later default it.
    /// </summary>
    public IReadOnlyList<string> DefaultTicketGrantingServiceEncryptionTypes
    {
        get
        {
            string? written = FirstValue(LibraryDefaults, "default_tgs_enctypes");
            return written is null ? PermittedEncryptionTypes : SplitEncryptionTypes(written);
        }
    }

    /// <summary>
    /// Gets <c>[libdefaults] allow_weak_crypto</c>: whether weak encryption types may be
    /// listed; <see langword="false" /> when unset or not one of MIT's boolean words.
    /// </summary>
    public bool AllowWeakCrypto => ParseBoolean(FirstValue(LibraryDefaults, "allow_weak_crypto")) ?? false;

    /// <summary>Gets every value of the relation <paramref name="path" /> names, in file order.</summary>
    /// <param name="path">The section, any groups, then the relation's tag, e.g. <c>realms</c>, <c>EXAMPLE.COM</c>, <c>kdc</c>.</param>
    /// <returns>The values; empty when the relation is not set.</returns>
    public IReadOnlyList<string> GetValues(params string[] path)
    {
        IEnumerable<KerberosConfigurationNode> groups = [Root];
        foreach (string groupName in path[..^1])
        {
            groups = groups.SelectMany(group => group.Children).Where(child => child.Value is null && child.Name == groupName);
        }

        return [.. groups.SelectMany(group => group.Children).Where(child => child.Name == path[^1]).Select(child => child.Value).OfType<string>()];
    }

    /// <summary>Gets the <c>kdc</c> entries of <c>[realms]</c> <paramref name="realm" />, in file order.</summary>
    /// <param name="realm">The realm, matched case-sensitively.</param>
    /// <returns>The entries as written, e.g. <c>kdc1.example.com:88</c>; empty when there are none.</returns>
    public IReadOnlyList<string> KdcEntries(string realm) => GetValues("realms", realm, "kdc");

    /// <summary>
    /// Maps <paramref name="host" /> to its realm as MIT's profile module does: the host,
    /// lower-cased and without a trailing dot, is looked up in <c>[domain_realm]</c>, then
    /// each suffix from each dot (<c>.example.com</c>, then <c>example.com</c>); failing
    /// that, <see cref="DefaultRealm" />.
    /// </summary>
    /// <param name="host">The host name.</param>
    /// <returns>The realm, or <see langword="null" /> when neither maps it.</returns>
    public string? RealmOfHost(string host)
    {
        string cleaned = host.ToLowerInvariant();
        cleaned = cleaned.EndsWith('.') ? cleaned[..^1] : cleaned;
        for (int start = 0; start >= 0; start = NextSuffixStart(cleaned, start))
        {
            string? realm = FirstValue("domain_realm", cleaned[start..]);
            if (realm is not null)
            {
                return realm;
            }
        }

        return DefaultRealm;
    }

    /// <summary>Parses a boolean as MIT's <c>profile_parse_boolean</c> does.</summary>
    /// <returns>The value, or <see langword="null" /> when unset or not one of MIT's words.</returns>
    internal static bool? ParseBoolean(string? value) =>
        value is null ? null
        : TrueWords.Contains(value, StringComparer.OrdinalIgnoreCase) ? true
        : FalseWords.Contains(value, StringComparer.OrdinalIgnoreCase) ? false
        : null;

    /// <summary>
    /// Parses an integer as MIT's <c>profile_parse_int</c> does, with <c>strtol</c> in base 0:
    /// an optional sign, then <c>0x</c> hexadecimal, <c>0</c> octal or decimal digits.
    /// </summary>
    /// <returns>The value, or <see langword="null" /> when unset, empty, not wholly a number, or out of range.</returns>
    internal static long? ParseInteger(string? value) =>
        string.IsNullOrEmpty(value) ? null : ParseSigned(value);

    private static long? ParseSigned(string value) => value[0] switch
    {
        '-' => -ParseUnsigned(value[1..]),
        '+' => ParseUnsigned(value[1..]),
        _ => ParseUnsigned(value),
    };

    private static long? ParseUnsigned(string digits)
    {
        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ParseDigits(digits[2..], 16);
        }

        return digits.Length > 1 && digits[0] == '0' ? ParseDigits(digits[1..], 8) : ParseDigits(digits, 10);
    }

    private static long? ParseDigits(string digits, int radix)
    {
        if (digits.Length == 0)
        {
            return null;
        }

        long total = 0;
        foreach (char digit in digits)
        {
            int place = HexadecimalDigits.IndexOf(char.ToLowerInvariant(digit), StringComparison.Ordinal);
            if (place < 0 || place >= radix || total > (int.MaxValue - place) / radix)
            {
                return null;
            }

            total = (total * radix) + place;
        }

        return total;
    }

    private static int NextSuffixStart(string host, int start) =>
        start < host.Length && host[start] == '.' ? start + 1 : host.IndexOf('.', start);

    private static string[] SplitEncryptionTypes(string written) =>
        written.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);

    private string? FirstValue(params string[] path)
    {
        IReadOnlyList<string> values = GetValues(path);
        return values.Count > 0 ? values[0] : null;
    }
}

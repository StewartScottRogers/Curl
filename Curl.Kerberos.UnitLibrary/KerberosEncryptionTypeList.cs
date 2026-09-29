namespace Curl.Kerberos;

/// <summary>
/// Resolves the words of a <c>krb5.conf</c> encryption type relation (<c>permitted_enctypes</c>,
/// <c>default_tkt_enctypes</c>, <c>default_tgs_enctypes</c>) to encryption type numbers as MIT
/// 1.22's <c>krb5int_parse_enctype_list</c> does (ADR-0209): each word, matched without regard
/// to case, is <c>DEFAULT</c> (MIT's default list), a family (<c>aes</c>, <c>camellia</c>,
/// <c>rc4</c>, <c>des3</c>) or a type's name or alias; a leading <c>-</c> removes its types, a
/// leading <c>+</c> or none appends those not yet listed, and a word MIT does not know is skipped.
/// </summary>
public static class KerberosEncryptionTypeList
{
    /// <summary>The weak type <c>arcfour-hmac-exp</c>, listed only when <c>allow_weak_crypto</c> is set.</summary>
    public const int WeakArcfourExport = 24;

    private const string DefaultWord = "DEFAULT";

    private static readonly Dictionary<string, int[]> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        ["des3"] = [16],
        ["aes"] = [18, 17, 20, 19],
        ["rc4"] = [23],
        ["camellia"] = [26, 25],
    };

    private static readonly Dictionary<string, int> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["des3-cbc-sha1"] = 16,
        ["des3-hmac-sha1"] = 16,
        ["des3-cbc-sha1-kd"] = 16,
        ["aes128-cts-hmac-sha1-96"] = 17,
        ["aes128-cts"] = 17,
        ["aes128-sha1"] = 17,
        ["aes256-cts-hmac-sha1-96"] = 18,
        ["aes256-cts"] = 18,
        ["aes256-sha1"] = 18,
        ["aes128-cts-hmac-sha256-128"] = 19,
        ["aes128-sha2"] = 19,
        ["aes256-cts-hmac-sha384-192"] = 20,
        ["aes256-sha2"] = 20,
        ["arcfour-hmac"] = 23,
        ["rc4-hmac"] = 23,
        ["arcfour-hmac-md5"] = 23,
        ["arcfour-hmac-exp"] = WeakArcfourExport,
        ["rc4-hmac-exp"] = WeakArcfourExport,
        ["arcfour-hmac-md5-exp"] = WeakArcfourExport,
        ["camellia128-cts-cmac"] = 25,
        ["camellia128-cts"] = 25,
        ["camellia256-cts-cmac"] = 26,
        ["camellia256-cts"] = 26,
    };

    /// <summary>
    /// Gets MIT 1.22's default list, what <c>DEFAULT</c> stands for: the AES types, then
    /// Camellia, as MIT 1.22.1's <c>kinit</c> was measured to offer them.
    /// </summary>
    public static IReadOnlyList<int> MitDefault { get; } = [18, 17, 20, 19, 25, 26];

    /// <summary>Resolves <paramref name="words" /> to encryption type numbers, in order of preference.</summary>
    /// <param name="words">The relation's words as <see cref="KerberosConfiguration" /> splits them, e.g. <c>DEFAULT</c>, <c>-rc4</c>.</param>
    /// <param name="allowWeakCrypto">Whether <c>allow_weak_crypto</c> is set, which lets <see cref="WeakArcfourExport" /> be listed.</param>
    /// <returns>The numbers; empty when no word names a type MIT knows.</returns>
    public static IReadOnlyList<int> Resolve(IEnumerable<string> words, bool allowWeakCrypto)
    {
        ArgumentNullException.ThrowIfNull(words);
        List<int> types = [];
        foreach (string word in words)
        {
            bool remove = word.StartsWith('-');
            string name = word.StartsWith('-') || word.StartsWith('+') ? word[1..] : word;
            foreach (int type in TypesNamed(name).Where(type => allowWeakCrypto || type != WeakArcfourExport))
            {
                Modify(types, type, remove);
            }
        }

        return types;
    }

    private static IEnumerable<int> TypesNamed(string name) =>
        name.Equals(DefaultWord, StringComparison.OrdinalIgnoreCase) ? MitDefault
        : Families.TryGetValue(name, out int[]? family) ? family
        : Names.TryGetValue(name, out int type) ? [type]
        : [];

    private static void Modify(List<int> types, int type, bool remove)
    {
        if (remove)
        {
            types.Remove(type);
        }
        else if (!types.Contains(type))
        {
            types.Add(type);
        }
    }
}

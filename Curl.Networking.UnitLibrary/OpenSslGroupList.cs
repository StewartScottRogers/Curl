using System.Collections.Frozen;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Reads curl's <c>--curves</c> value as OpenSSL 3.5's <c>SSL_CTX_set1_groups_list</c> reads
/// it (measured against Ubuntu's curl 8.18.0 with OpenSSL 3.5.5, BL-709, ADR-0284): group
/// names matched without regard to case and separated by <c>:</c> or <c>/</c>; a <c>?</c>
/// prefix drops an unknown name instead of failing, <c>*</c> asks for a key share, <c>-</c>
/// removes a group named earlier, and <c>DEFAULT</c> stands for the platform curl's own
/// groups and key shares. A repeated group is offered once. Without a <c>*</c>, the first
/// group the client can make a key share for gets the one key share; with one, only the
/// starred TLS 1.3 groups get key shares, so stars on TLS 1.2 groups alone leave none.
/// </summary>
internal static class OpenSslGroupList
{
    private const string DefaultKeyword = "DEFAULT";

    private static readonly char[] EntrySeparators = [':', '/'];

    // OpenSSL 3.5's names for every group it offers in a ClientHello, each one Curl.Tls can
    // use (BL-1049 added the brainpool tls13 curves, pure ML-KEM and the NIST-curve hybrids).
    private static readonly FrozenDictionary<string, ushort> GroupsByName = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
    {
        ["secp256r1"] = TlsNamedGroup.Secp256r1,
        ["prime256v1"] = TlsNamedGroup.Secp256r1,
        ["P-256"] = TlsNamedGroup.Secp256r1,
        ["secp384r1"] = TlsNamedGroup.Secp384r1,
        ["P-384"] = TlsNamedGroup.Secp384r1,
        ["secp521r1"] = TlsNamedGroup.Secp521r1,
        ["P-521"] = TlsNamedGroup.Secp521r1,
        ["brainpoolP256r1"] = TlsNamedGroup.BrainpoolP256r1,
        ["brainpoolP384r1"] = TlsNamedGroup.BrainpoolP384r1,
        ["brainpoolP512r1"] = TlsNamedGroup.BrainpoolP512r1,
        ["brainpoolP256r1tls13"] = TlsNamedGroup.BrainpoolP256r1Tls13,
        ["brainpoolP384r1tls13"] = TlsNamedGroup.BrainpoolP384r1Tls13,
        ["brainpoolP512r1tls13"] = TlsNamedGroup.BrainpoolP512r1Tls13,
        ["x25519"] = TlsNamedGroup.X25519,
        ["x448"] = TlsNamedGroup.X448,
        ["ffdhe2048"] = TlsNamedGroup.Ffdhe2048,
        ["ffdhe3072"] = TlsNamedGroup.Ffdhe3072,
        ["ffdhe4096"] = TlsNamedGroup.Ffdhe4096,
        ["ffdhe6144"] = TlsNamedGroup.Ffdhe6144,
        ["ffdhe8192"] = TlsNamedGroup.Ffdhe8192,
        ["MLKEM512"] = TlsNamedGroup.MlKem512,
        ["MLKEM768"] = TlsNamedGroup.MlKem768,
        ["MLKEM1024"] = TlsNamedGroup.MlKem1024,
        ["SecP256r1MLKEM768"] = TlsNamedGroup.SecP256r1MlKem768,
        ["X25519MLKEM768"] = TlsNamedGroup.X25519MlKem768,
        ["SecP384r1MLKEM1024"] = TlsNamedGroup.SecP384r1MlKem1024,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a <c>--curves</c> value.</summary>
    /// <param name="value">The value, verbatim.</param>
    /// <param name="defaults">The platform curl's profile, whose groups <c>DEFAULT</c> names.</param>
    /// <returns>
    /// The groups to offer and the ones to send a key share for, both cut to what the client
    /// can use (so the groups may be empty), or <see langword="null" /> when OpenSSL refuses
    /// the value: an empty entry, an unknown name without <c>?</c>, or a prefixed <c>DEFAULT</c>.
    /// </returns>
    public static OfferedGroups? Parse(string value, ClientHelloProfile defaults)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(defaults);

        var groups = new List<ushort>();
        var keyShares = new List<ushort>();
        foreach (var entry in value.Split(EntrySeparators))
        {
            if (!Apply(entry, defaults, groups, keyShares))
            {
                return null;
            }
        }

        ushort[] usable = [.. groups.Where(IsUsable)];
        // Every starred group was offered too (a removal takes it out of both lists), but only
        // a TLS 1.3 group gets a key share: stars on TLS 1.2 groups alone leave none, which
        // OpenSSL refuses as "no suitable key share" when it offers TLS 1.3 (BL-1082).
        return new OfferedGroups(usable, keyShares.Count > 0 ? [.. keyShares.Where(TlsNamedGroup.CanShare)] : FirstWithKeyShare(usable));
    }

    // The first group a TLS 1.3 key share can be made for, or none.
    private static ushort[] FirstWithKeyShare(ushort[] groups)
    {
        foreach (var group in groups)
        {
            if (TlsNamedGroup.CanShare(group))
            {
                return [group];
            }
        }

        return [];
    }

    // Applies one entry to the lists, or returns false when OpenSSL refuses it.
    private static bool Apply(string entry, ClientHelloProfile defaults, List<ushort> groups, List<ushort> keyShares)
    {
        var name = entry.TrimStart('-', '?', '*');
        var prefixes = entry[..^name.Length];
        if (string.Equals(entry, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
        {
            AddAll(groups, defaults.SupportedGroups);
            AddAll(keyShares, defaults.KeyShareGroups);
            return true;
        }

        if (!GroupsByName.TryGetValue(name, out var group))
        {
            return prefixes.Contains('?');
        }

        if (prefixes.Contains('-'))
        {
            groups.Remove(group);
            keyShares.Remove(group);
            return true;
        }

        AddAll(groups, [group]);
        AddAll(keyShares, prefixes.Contains('*') ? [group] : []);
        return true;
    }

    private static void AddAll(List<ushort> list, IEnumerable<ushort> groups) =>
        list.AddRange(groups.Where(group => !list.Contains(group)));

    // A group the TLS 1.3 client can share a key for or the TLS 1.2 client agrees ECDHE on.
    private static bool IsUsable(ushort group) => TlsNamedGroup.CanShare(group) || TlsNamedGroup.IsTls12EcdheGroup(group);
}

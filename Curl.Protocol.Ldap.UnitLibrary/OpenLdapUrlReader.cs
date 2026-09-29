using System.Collections.Frozen;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Reads an LDAP URL as the OpenLDAP build of curl does, with <c>libldap</c>'s
/// <c>ldap_url_parse</c> (ADR-0166, measured by BL-587 against curl 8.18.0 with OpenLDAP 2.6.10).
/// </summary>
/// <remarks>
/// Each part is percent-decoded whole before it is read, and ends at a decoded zero byte; a
/// <c>%</c> not followed by two hex digits empties the part, so a DN or attribute list with
/// one is sent empty and a filter with one is refused. Attributes are split at commas, empty
/// ones skipped. The scope is <c>base</c>, <c>one</c>, <c>onelevel</c>, <c>sub</c>,
/// <c>subtree</c>, or <c>subord</c>, <c>subordinate</c> or <c>children</c> for RFC 4512's
/// subordinate subtree, in any case. Extensions are ignored, even critical ones, but an empty
/// extensions part or a fifth <c>?</c> refuses the URL, and so does user information in it.
/// </remarks>
internal static class OpenLdapUrlReader
{
    private const string BadUrl = "LDAP local: bad URL";

    private const string BadScope = "LDAP local: bad or missing scope";

    private const string BadFilter = "LDAP local: bad or missing filter";

    private const string BadExtensions = "LDAP local: bad or missing extensions";

    /// <summary>The most parts a query has: attributes, scope, filter and extensions.</summary>
    private const int MaxQueryParts = 4;

    /// <summary>The scopes <c>ldap_pvt_str2scope</c> reads, in any case.</summary>
    private static readonly FrozenDictionary<string, int> Scopes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["base"] = 0,
        ["one"] = 1,
        ["onelevel"] = 1,
        ["sub"] = 2,
        ["subtree"] = 2,
        ["subord"] = 3,
        ["subordinate"] = 3,
        ["children"] = 3,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads <paramref name="url" />.</summary>
    /// <param name="url">The <c>ldap</c> or <c>ldaps</c> URL.</param>
    /// <returns>The search it names, or exit 3 with the build's message.</returns>
    public static LdapUrlReading Read(CurlUrl url)
    {
        if (url.User is not null || url.Password is not null)
        {
            return LdapUrlReading.Refused(BadUrl);
        }

        string baseObject = Decode(LdapWireText.ToByteString(url.AbsolutePath[1..]));
        return url.Query is null
            ? new LdapUrlReading(new LdapSearchParameters(baseObject, [], 0, null), null)
            : ReadQuery(baseObject, LdapWireText.ToByteString(url.Query).Split('?'));
    }

    private static LdapUrlReading ReadQuery(string baseObject, string[] parts)
    {
        string[] attributes = Decode(parts[0]).Split(',', StringSplitOptions.RemoveEmptyEntries);
        string scopePart = PartAt(parts, 1);
        int scope = scopePart.Length == 0 ? 0 : Scopes.GetValueOrDefault(Decode(scopePart), -1);
        return scope < 0
            ? LdapUrlReading.Refused(BadScope)
            : ReadFilter(new LdapSearchParameters(baseObject, attributes, scope, null), parts);
    }

    private static LdapUrlReading ReadFilter(LdapSearchParameters search, string[] parts)
    {
        string filterPart = PartAt(parts, 2);
        if (filterPart.Length == 0)
        {
            return ReadExtensions(search, parts);
        }

        string filter = Decode(filterPart);
        return filter.Length == 0
            ? LdapUrlReading.Refused(BadFilter)
            : ReadExtensions(search with { Filter = filter }, parts);
    }

    /// <summary>Refuses a fifth <c>?</c>, or a <c>?</c> that starts the extensions with nothing after it.</summary>
    private static LdapUrlReading ReadExtensions(LdapSearchParameters search, string[] parts)
    {
        if (parts.Length > MaxQueryParts)
        {
            return LdapUrlReading.Refused(BadUrl);
        }

        return parts.Length == MaxQueryParts && parts[3].Length == 0
            ? LdapUrlReading.Refused(BadExtensions)
            : new LdapUrlReading(search, null);
    }

    /// <summary>The query part at <paramref name="index" />, or the empty string when the query has fewer.</summary>
    private static string PartAt(string[] parts, int index) => index < parts.Length ? parts[index] : string.Empty;

    /// <summary>Percent-decodes as <c>ldap_pvt_hex_unescape</c> leaves a part.</summary>
    /// <returns>The decoded byte string up to its first zero byte, or the empty string when a <c>%</c> is not followed by two hex digits.</returns>
    private static string Decode(string part)
    {
        var decoded = new System.Text.StringBuilder(part.Length);
        for (int index = 0; index < part.Length; index++)
        {
            if (LdapHexEscape.TryRead(part, index, '%', out char octet))
            {
                decoded.Append(octet);
                index += 2;
            }
            else if (part[index] == '%')
            {
                return string.Empty;
            }
            else
            {
                decoded.Append(part[index]);
            }
        }

        string text = decoded.ToString();
        int zero = text.IndexOf('\0', StringComparison.Ordinal);
        return zero < 0 ? text : text[..zero];
    }
}

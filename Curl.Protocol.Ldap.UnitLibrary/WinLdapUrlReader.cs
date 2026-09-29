using System.Collections.Frozen;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Reads an LDAP URL as the Windows build of curl 8.21.0 does, with its own
/// <c>ldap_url_parse2_low</c> in <c>lib/ldap.c</c> (ADR-0166, measured by BL-587).
/// </summary>
/// <remarks>
/// Each part is percent-decoded as <c>Curl_urldecode</c> decodes: <c>%</c> and two hex digits
/// become a byte, any other <c>%</c> stays as it is, and a decoded zero byte refuses the URL
/// with <c>No Memory</c>. Attributes are split at commas before decoding and stop at the first
/// empty one; the scope is not decoded and is <c>base</c>, <c>one</c>, <c>onetree</c>,
/// <c>sub</c> or <c>subtree</c> in any case; extensions are ignored, and so is everything after
/// them, but a <c>?</c> with nothing after it where they would start refuses the URL.
/// </remarks>
internal static class WinLdapUrlReader
{
    private const string InvalidSyntax = "Bad LDAP URL: Invalid Syntax";

    private const string NoMemory = "Bad LDAP URL: No Memory";

    /// <summary>The longest attribute <c>curlx_str_until</c> takes; a longer one ends the list.</summary>
    private const int MaxAttributeLength = 1024;

    /// <summary>The scopes <c>str2scope</c> reads, in any case; an empty part is the default, <c>base</c>.</summary>
    private static readonly FrozenDictionary<string, int> Scopes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [string.Empty] = 0,
        ["base"] = 0,
        ["one"] = 1,
        ["onetree"] = 1,
        ["sub"] = 2,
        ["subtree"] = 2,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads <paramref name="url" />.</summary>
    /// <param name="url">The <c>ldap</c> or <c>ldaps</c> URL.</param>
    /// <returns>The search it names, or exit 3 with the build's message.</returns>
    public static LdapUrlReading Read(CurlUrl url)
    {
        string? baseObject = Decode(LdapWireText.ToByteString(url.AbsolutePath[1..]));
        if (baseObject is null)
        {
            return LdapUrlReading.Refused(NoMemory);
        }

        return url.Query is null
            ? new LdapUrlReading(new LdapSearchParameters(baseObject, [], 0, null), null)
            : ReadQuery(baseObject, LdapWireText.ToByteString(url.Query).Split('?', 4));
    }

    private static LdapUrlReading ReadQuery(string baseObject, string[] parts)
    {
        List<string>? attributes = ReadAttributes(parts[0]);
        return attributes is null
            ? LdapUrlReading.Refused(NoMemory)
            : ReadScopeAndFilter(baseObject, attributes, parts);
    }

    private static LdapUrlReading ReadScopeAndFilter(string baseObject, List<string> attributes, string[] parts)
    {
        int scope = Scopes.GetValueOrDefault(PartAt(parts, 1), -1);
        return scope < 0
            ? LdapUrlReading.Refused(InvalidSyntax)
            : ReadFilter(new LdapSearchParameters(baseObject, attributes, scope, null), parts);
    }

    private static LdapUrlReading ReadFilter(LdapSearchParameters search, string[] parts)
    {
        string? filter = Decode(PartAt(parts, 2));
        if (filter is null)
        {
            return LdapUrlReading.Refused(NoMemory);
        }

        return HasEmptyExtensions(parts)
            ? LdapUrlReading.Refused(InvalidSyntax)
            : new LdapUrlReading(search with { Filter = filter.Length > 0 ? filter : null }, null);
    }

    /// <summary>The query part at <paramref name="index" />, or the empty string when the query has fewer.</summary>
    private static string PartAt(string[] parts, int index) => index < parts.Length ? parts[index] : string.Empty;

    /// <summary>Whether a <c>?</c> starts the extensions and nothing follows it.</summary>
    private static bool HasEmptyExtensions(string[] parts) => parts.Length > 3 && parts[3].Length == 0;

    /// <summary>Splits and decodes the attributes, stopping at the first empty or over-long one.</summary>
    /// <returns>The attributes, or <see langword="null" /> when one decodes to a zero byte.</returns>
    private static List<string>? ReadAttributes(string part)
    {
        List<string> attributes = [];
        foreach (string attribute in part.Split(','))
        {
            if (attribute.Length is 0 or > MaxAttributeLength)
            {
                break;
            }

            string? decoded = Decode(attribute);
            if (decoded is null)
            {
                return null;
            }

            attributes.Add(decoded);
        }

        return attributes;
    }


    /// <summary>Percent-decodes as <c>Curl_urldecode</c> with <c>REJECT_ZERO</c> does.</summary>
    /// <returns>The decoded byte string, or <see langword="null" /> when it holds a zero byte.</returns>
    private static string? Decode(string part)
    {
        var decoded = new System.Text.StringBuilder(part.Length);
        for (int index = 0; index < part.Length; index++)
        {
            if (LdapHexEscape.TryRead(part, index, '%', out char octet))
            {
                decoded.Append(octet);
                index += 2;
            }
            else
            {
                decoded.Append(part[index]);
            }
        }

        string text = decoded.ToString();
        return text.Contains('\0', StringComparison.Ordinal) ? null : text;
    }
}

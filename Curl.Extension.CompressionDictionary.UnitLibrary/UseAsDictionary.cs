using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// The parsed <c>Use-As-Dictionary</c> response header field (RFC 9842 section 2.1): the URL pattern the
/// dictionary applies to, the request destinations it is limited to and its server-chosen identifier.
/// </summary>
/// <param name="Match">The <c>match</c> URL pattern, resolved later against the dictionary's URL (section 2.1.1).</param>
/// <param name="MatchDestinations">The <c>match-dest</c> request destinations (section 2.1.2); empty when absent.</param>
/// <param name="Id">The <c>id</c> the client echoes in <c>Dictionary-ID</c> (section 2.1.3); empty when absent.</param>
public sealed record UseAsDictionary(string Match, IReadOnlyList<string> MatchDestinations, string Id)
{
    /// <summary>The longest <c>id</c> a client must accept (RFC 9842 section 2.1.3).</summary>
    public const int MaximumIdLength = 1024;

    /// <summary>
    /// Parses a <c>Use-As-Dictionary</c> field value; null when the field must be ignored: it is not an RFC 8941
    /// dictionary, has no sf-string <c>match</c>, has a <c>match-dest</c> that is not an inner list of sf-strings,
    /// an <c>id</c> that is not an sf-string of at most 1024 characters, or a <c>type</c> other than the token
    /// <c>raw</c> (RFC 9842 sections 2.1 to 2.1.4). Unknown members are ignored.
    /// </summary>
    public static UseAsDictionary? Parse(string fieldValue)
    {
        var members = StructuredFieldDictionaryParser.Parse(fieldValue);
        if (!IsRawType(members))
        {
            return null;
        }

        var match = ReadString(members, "match", defaultValue: null);
        var id = ReadString(members, "id", defaultValue: string.Empty);
        var destinations = ReadDestinations(members);
        if (match is null || !IsValidId(id) || destinations is null)
        {
            return null;
        }

        return new UseAsDictionary(match, destinations, id);
    }

    private static bool IsRawType([NotNullWhen(true)] IReadOnlyDictionary<string, StructuredFieldMember>? members) =>
        members is not null
        && (!members.TryGetValue("type", out var type) || type.Item is { Kind: StructuredFieldItemKind.Token, Value: "raw" });

    private static bool IsValidId([NotNullWhen(true)] string? id) => id is not null && id.Length <= MaximumIdLength;

    private static string? ReadString(IReadOnlyDictionary<string, StructuredFieldMember> members, string key, string? defaultValue)
    {
        if (!members.TryGetValue(key, out var member))
        {
            return defaultValue;
        }

        return member.Item is { Kind: StructuredFieldItemKind.String } item ? item.Value : null;
    }

    private static string[]? ReadDestinations(IReadOnlyDictionary<string, StructuredFieldMember> members)
    {
        if (!members.TryGetValue("match-dest", out var member))
        {
            return [];
        }

        var items = member.InnerList;
        if (items is null || items.Any(item => item.Kind != StructuredFieldItemKind.String))
        {
            return null;
        }

        return [.. items.Select(item => item.Value)];
    }
}

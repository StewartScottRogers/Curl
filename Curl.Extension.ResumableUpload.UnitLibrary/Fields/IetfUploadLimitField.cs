using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>
/// The resumable upload draft's <c>Upload-Limit</c> field: an sf-dictionary of non-negative sf-integers keyed <c>max-size</c>,
/// <c>min-size</c>, <c>max-append-size</c>, <c>min-append-size</c> and <c>expires</c> (draft-ietf-httpbis-resumable-upload §8.2).
/// Unknown keys are ignored, as the draft requires; a later member with the same key replaces an earlier one (RFC 9651 §3.2).
/// </summary>
public static class IetfUploadLimitField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Limit";

    private static readonly SearchValues<char> KeyCharacters = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789_-.*");

    /// <summary>Formats the field value from the limits <paramref name="limit"/> states, in the order the draft lists the keys.</summary>
    /// <param name="limit">The limits; at least one must be stated.</param>
    /// <returns>The sf-dictionary, such as <c>max-size=1000, expires=3600</c>.</returns>
    public static string Format(ResumableUploadLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        var members = new StringBuilder();
        AppendMember(members, "max-size", limit.MaxSize);
        AppendMember(members, "min-size", limit.MinSize);
        AppendMember(members, "max-append-size", limit.MaxAppendSize);
        AppendMember(members, "min-append-size", limit.MinAppendSize);
        AppendMember(members, "expires", limit.Expires);
        return members.Length > 0 ? members.ToString() : throw new ArgumentException("An Upload-Limit states at least one limit.", nameof(limit));
    }

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="limit">The limits, or <see langword="null"/> when the value is malformed.</param>
    /// <returns>Whether the value is an sf-dictionary whose known keys hold non-negative sf-integers.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out ResumableUploadLimit? limit)
    {
        limit = null;
        if (value is null)
        {
            return false;
        }

        var parsed = new ResumableUploadLimit();
        foreach (var member in value.Split(','))
        {
            if (!TryApplyMember(member.Trim(' ', '\t'), ref parsed))
            {
                return false;
            }
        }

        limit = parsed;
        return true;
    }

    private static void AppendMember(StringBuilder members, string key, long? value)
    {
        if (value is { } stated)
        {
            members.Append(members.Length > 0 ? ", " : string.Empty).Append(key).Append('=').Append(StructuredFieldItem.FormatInteger(stated, key));
        }
    }

    private static bool TryApplyMember(string member, ref ResumableUploadLimit limit)
    {
        var keyEnd = member.AsSpan().IndexOfAny('=', ';');
        var key = keyEnd < 0 ? member : member[..keyEnd];
        if (!IsKey(key))
        {
            return false;
        }

        if (!IsKnownKey(key))
        {
            return true;
        }

        if (!TryParseMemberInteger(member, keyEnd, out var stated))
        {
            return false;
        }

        limit = WithMember(limit, key, stated);
        return true;
    }

    // A member without "=" is the boolean true (RFC 9651 §3.2), never an integer.
    private static bool TryParseMemberInteger(string member, int keyEnd, out long stated)
    {
        stated = 0;
        return keyEnd >= 0 && member[keyEnd] == '=' && StructuredFieldItem.TryParseInteger(member.AsSpan(keyEnd + 1), out stated);
    }

    private static bool IsKnownKey(string key) => key is "max-size" or "min-size" or "max-append-size" or "min-append-size" or "expires";

    private static ResumableUploadLimit WithMember(ResumableUploadLimit limit, string key, long stated) => key switch
    {
        "max-size" => limit with { MaxSize = stated },
        "min-size" => limit with { MinSize = stated },
        "max-append-size" => limit with { MaxAppendSize = stated },
        "min-append-size" => limit with { MinAppendSize = stated },
        _ => limit with { Expires = stated },
    };

    // RFC 9651 §3.1.2: key = ( lcalpha / "*" ) *( lcalpha / DIGIT / "_" / "-" / "." / "*" ).
    private static bool IsKey(string key) =>
        key.Length > 0 && (char.IsAsciiLetterLower(key[0]) || key[0] == '*') && key.AsSpan().IndexOfAnyExcept(KeyCharacters) < 0;
}

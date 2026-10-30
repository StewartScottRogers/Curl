using System.Buffers;

namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>Formats and parses the comma-separated lists of tus 1.0's <c>Tus-Version</c> and <c>Tus-Extension</c> fields.</summary>
internal static class TusList
{
    /// <summary><see cref="IsVersion"/> as one delegate, so callers share it.</summary>
    public static readonly Func<string, bool> VersionItem = IsVersion;

    /// <summary><see cref="IsToken"/> as one delegate, so callers share it.</summary>
    public static readonly Func<string, bool> TokenItem = IsToken;

    // RFC 9110 §5.6.2: tchar.
    private static readonly SearchValues<char> TokenCharacters =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    /// <summary>Joins <paramref name="items"/> with commas, refusing an empty list or an item <paramref name="isItem"/> refuses.</summary>
    /// <param name="items">The items.</param>
    /// <param name="isItem">Whether a string is a valid item.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <returns>The field value.</returns>
    public static string Format(IReadOnlyList<string> items, Func<string, bool> isItem, string paramName)
    {
        ArgumentNullException.ThrowIfNull(items, paramName);
        return items.Count > 0 && items.All(isItem)
            ? string.Join(',', items)
            : throw new ArgumentException("The list is empty or holds an item that is not valid.", paramName);
    }

    /// <summary>Splits a comma-separated list, trimming the whitespace around each item.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="isItem">Whether a string is a valid item.</param>
    /// <param name="items">The items, or an empty list when the value is malformed.</param>
    /// <returns>Whether every item is valid.</returns>
    public static bool TryParse(string? value, Func<string, bool> isItem, out IReadOnlyList<string> items)
    {
        var parsed = value?.Split(',', StringSplitOptions.TrimEntries) ?? [];
        items = parsed.Length > 0 && parsed.All(isItem) ? parsed : [];
        return items.Count > 0;
    }

    /// <summary>Whether <paramref name="version"/> is dot-separated decimal numbers, such as <c>1.0.0</c>.</summary>
    /// <param name="version">The candidate.</param>
    /// <returns><see langword="true"/> for a version.</returns>
    public static bool IsVersion(string version) =>
        version.Split('.').All(part => part.Length > 0 && part.AsSpan().IndexOfAnyExceptInRange('0', '9') < 0);

    /// <summary>Whether <paramref name="name"/> is an RFC 9110 §5.6.2 token, such as <c>creation-with-upload</c>.</summary>
    /// <param name="name">The candidate.</param>
    /// <returns><see langword="true"/> for a token.</returns>
    public static bool IsToken(string name) =>
        name.Length > 0 && name.AsSpan().IndexOfAnyExcept(TokenCharacters) < 0;
}

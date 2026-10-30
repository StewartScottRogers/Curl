using System.Globalization;

namespace Curl.Extension.SegmentedDownload;

/// <summary>
/// A <c>Content-Range</c> of a 206 response (RFC 9110 §14.4): <c>bytes first-last/complete-length</c>. An
/// unsatisfied range (<c>*/length</c>), an unknown complete length (<c>/*</c>) or any other unit is not one.
/// </summary>
/// <param name="Range">The bytes the response carries.</param>
/// <param name="CompleteLength">The selected representation's length.</param>
internal sealed record ContentRange(SegmentRange Range, long CompleteLength)
{
    private const string Prefix = "bytes ";

    /// <summary>Reads a <c>Content-Range</c> field value.</summary>
    /// <param name="value">The field value, or <see langword="null" /> when the response had none.</param>
    /// <returns>The range, or <see langword="null" /> when the value is not a satisfied byte range with a known complete length.</returns>
    public static ContentRange? Parse(string? value)
    {
        if (value is null || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Spec(value.AsSpan(Prefix.Length).Trim());
    }

    private static ContentRange? Spec(ReadOnlySpan<char> rest)
    {
        var slash = rest.IndexOf('/');
        var dash = rest.IndexOf('-');
        if (slash < 0 || dash < 0 || dash > slash)
        {
            return null;
        }

        return Numbers(rest[..dash], rest[(dash + 1)..slash], rest[(slash + 1)..]);
    }

    private static ContentRange? Numbers(ReadOnlySpan<char> firstDigits, ReadOnlySpan<char> lastDigits, ReadOnlySpan<char> completeDigits) =>
        Number(firstDigits) is { } first && Number(lastDigits) is { } last && Number(completeDigits) is { } complete
            ? Satisfied(first, last, complete)
            : null;

    private static ContentRange? Satisfied(long first, long last, long complete) =>
        first <= last && last < complete ? new ContentRange(new SegmentRange(first, last), complete) : null;

    private static long? Number(ReadOnlySpan<char> digits) =>
        long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}

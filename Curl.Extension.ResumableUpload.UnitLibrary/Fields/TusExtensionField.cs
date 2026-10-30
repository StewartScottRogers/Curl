namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>tus 1.0's <c>Tus-Extension</c> field: the comma-separated extensions the server supports, such as <c>creation,termination</c> (tus 1.0.0, "Tus-Extension").</summary>
public static class TusExtensionField
{
    /// <summary>The field name.</summary>
    public const string Name = "Tus-Extension";

    /// <summary>Formats the field value.</summary>
    /// <param name="items">The items, at least one, each a token such as <c>creation</c>.</param>
    /// <returns>The items joined with commas.</returns>
    public static string Format(IReadOnlyList<string> items) => TusList.Format(items, TusList.TokenItem, nameof(items));

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="items">The items, or an empty list when the value is malformed.</param>
    /// <returns>Whether the value is a non-empty comma-separated list of valid items.</returns>
    public static bool TryParse(string? value, out IReadOnlyList<string> items) => TusList.TryParse(value, TusList.TokenItem, out items);
}

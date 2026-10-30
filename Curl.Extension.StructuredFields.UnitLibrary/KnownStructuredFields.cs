namespace Curl.Extension.StructuredFields;

/// <summary>
/// The response fields whose specifications define them as Structured Fields, each with the top-level type its
/// specification gives (the "Structured Type" column of IANA's HTTP Field Name Registry, RFC 9110 §16.3.1), and
/// the readable line for one such field's value (ADR-1149).
/// </summary>
internal static class KnownStructuredFields
{
    /// <summary>Gets each known field's name, as its specification writes it, and its top-level type.</summary>
    public static IReadOnlyDictionary<string, StructuredFieldType> Types { get; } = new Dictionary<string, StructuredFieldType>(StringComparer.OrdinalIgnoreCase)
    {
        ["Accept-CH"] = StructuredFieldType.List,
        ["Cache-Groups"] = StructuredFieldType.List,
        ["Cache-Status"] = StructuredFieldType.List,
        ["CDN-Cache-Control"] = StructuredFieldType.Dictionary,
        ["Content-Digest"] = StructuredFieldType.Dictionary,
        ["Critical-CH"] = StructuredFieldType.List,
        ["Cross-Origin-Embedder-Policy"] = StructuredFieldType.Item,
        ["Cross-Origin-Embedder-Policy-Report-Only"] = StructuredFieldType.Item,
        ["Cross-Origin-Opener-Policy"] = StructuredFieldType.Item,
        ["Cross-Origin-Opener-Policy-Report-Only"] = StructuredFieldType.Item,
        ["Origin-Agent-Cluster"] = StructuredFieldType.Item,
        ["Permissions-Policy"] = StructuredFieldType.Dictionary,
        ["Priority"] = StructuredFieldType.Dictionary,
        ["Proxy-Status"] = StructuredFieldType.List,
        ["Reporting-Endpoints"] = StructuredFieldType.Dictionary,
        ["Repr-Digest"] = StructuredFieldType.Dictionary,
        ["Signature"] = StructuredFieldType.Dictionary,
        ["Signature-Input"] = StructuredFieldType.Dictionary,
        ["Accept-Signature"] = StructuredFieldType.Dictionary,
        ["Use-As-Dictionary"] = StructuredFieldType.Dictionary,
        ["Want-Content-Digest"] = StructuredFieldType.Dictionary,
        ["Want-Repr-Digest"] = StructuredFieldType.Dictionary,
    };

    /// <summary>
    /// Gives the readable line for a field's value, or why it is not a valid value of its type, in which case
    /// RFC 9651 §4.2 has a recipient ignore it.
    /// </summary>
    /// <param name="value">The field's value, every field line of the name joined with commas.</param>
    /// <param name="type">The field's top-level type.</param>
    /// <returns>The readable line.</returns>
    public static string Describe(string value, StructuredFieldType type)
    {
        try
        {
            return type switch
            {
                StructuredFieldType.List => ReadableStructuredValue.OfList(StructuredFieldParser.ParseList(value)),
                StructuredFieldType.Dictionary => ReadableStructuredValue.OfDictionary(StructuredFieldParser.ParseDictionary(value)),
                _ => ReadableStructuredValue.OfItem(StructuredFieldParser.ParseItem(value)),
            };
        }
        catch (FormatException exception)
        {
            return $"not a valid {type} ({exception.Message}), ignored";
        }
    }
}

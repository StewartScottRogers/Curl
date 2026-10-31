namespace Curl.Extension.StructuredFields;

/// <summary>The three top-level types a Structured Field can be (RFC 9651 §3).</summary>
internal enum StructuredFieldType
{
    /// <summary>A List (§3.1).</summary>
    List,

    /// <summary>A Dictionary (§3.2).</summary>
    Dictionary,

    /// <summary>An Item (§3.3).</summary>
    Item,
}

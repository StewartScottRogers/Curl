namespace Curl.Http2;

/// <summary>
/// One header field in an HPACK header list (RFC 7541 section 1.3): a name, a value, and
/// whether it travels as a never-indexed literal. Names and values are byte strings; each
/// character stands for one byte (Latin-1), so a character above U+00FF cannot be encoded.
/// </summary>
/// <param name="Name">The field name, lowercase as HTTP/2 requires.</param>
/// <param name="Value">The field value.</param>
/// <param name="IsNeverIndexed">
/// Whether the field is sent, or was received, as a literal never indexed (RFC 7541
/// section 6.2.3), so that no intermediary adds it to a dynamic table.
/// </param>
public readonly record struct HeaderField(string Name, string Value, bool IsNeverIndexed = false)
{
    /// <summary>
    /// The per-entry overhead RFC 7541 section 4.1 adds to an entry's name and value length.
    /// </summary>
    internal const int EntryOverhead = 32;

    /// <summary>
    /// Gets the size this field occupies in a dynamic table (RFC 7541 section 4.1): the
    /// name's and value's lengths in bytes plus 32.
    /// </summary>
    public int Size => Name.Length + Value.Length + EntryOverhead;
}

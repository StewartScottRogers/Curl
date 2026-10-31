namespace Curl.Extension.CompressionDictionary;

/// <summary>The bare item types of an RFC 8941 structured field (section 3.3).</summary>
internal enum StructuredFieldItemKind
{
    /// <summary>An integer or decimal (sections 3.3.1 and 3.3.2).</summary>
    Number,

    /// <summary>An sf-string (section 3.3.3).</summary>
    String,

    /// <summary>A token (section 3.3.4).</summary>
    Token,

    /// <summary>A byte sequence (section 3.3.5).</summary>
    ByteSequence,

    /// <summary>A boolean (section 3.3.6).</summary>
    Boolean,
}

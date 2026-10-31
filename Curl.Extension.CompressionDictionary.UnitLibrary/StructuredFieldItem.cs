namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// One RFC 8941 bare item: its kind and its value as text (the unescaped string, the token,
/// the number as written, the base64 of a byte sequence, or "0" or "1" for a boolean).
/// Parameters are parsed for syntax and dropped: RFC 9842 defines none.
/// </summary>
internal sealed record StructuredFieldItem(StructuredFieldItemKind Kind, string Value);

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// One member value of an RFC 8941 dictionary (section 3.2): either a single item or an inner list
/// of items (section 3.1.1). Exactly one of the two is set.
/// </summary>
internal sealed record StructuredFieldMember(StructuredFieldItem? Item, IReadOnlyList<StructuredFieldItem>? InnerList);

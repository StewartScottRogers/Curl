namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>One field line of a Binary HTTP header or trailer section (RFC 9292 section 3.6).</summary>
/// <param name="Name">The field name; each character is one byte (Latin-1).</param>
/// <param name="Value">The field value; each character is one byte (Latin-1).</param>
public sealed record BinaryHttpField(string Name, string Value);

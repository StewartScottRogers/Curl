namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// A request to encode as a Binary HTTP message (RFC 9292): its control data (section 3.4),
/// header section and content. The encoder writes no trailers.
/// </summary>
/// <param name="Method">The request method, such as <c>GET</c>.</param>
/// <param name="Scheme">The scheme, such as <c>https</c>.</param>
/// <param name="Authority">The authority, or empty when the Host header field carries it.</param>
/// <param name="Path">The path and query, such as <c>/hello.txt</c>.</param>
/// <param name="Headers">The header field lines, in order.</param>
/// <param name="Content">The request content, empty for none.</param>
public sealed record BinaryHttpRequest(
    string Method,
    string Scheme,
    string Authority,
    string Path,
    IReadOnlyList<BinaryHttpField> Headers,
    ReadOnlyMemory<byte> Content);

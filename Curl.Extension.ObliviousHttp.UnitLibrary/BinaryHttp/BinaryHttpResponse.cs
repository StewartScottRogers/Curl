namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>A response decoded from a Binary HTTP message (RFC 9292 sections 3.5 to 3.7).</summary>
/// <param name="InformationalResponses">The informational responses before the final one, in order.</param>
/// <param name="StatusCode">The final status code, 200 to 599.</param>
/// <param name="Headers">The final response's header field lines, in order.</param>
/// <param name="Content">The content, empty when the message has none or is truncated before it.</param>
/// <param name="Trailers">The trailer field lines, empty when the message has none or is truncated before them.</param>
public sealed record BinaryHttpResponse(
    IReadOnlyList<BinaryHttpInformationalResponse> InformationalResponses,
    int StatusCode,
    IReadOnlyList<BinaryHttpField> Headers,
    ReadOnlyMemory<byte> Content,
    IReadOnlyList<BinaryHttpField> Trailers);

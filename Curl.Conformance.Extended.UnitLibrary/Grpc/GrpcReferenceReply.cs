using Curl.Http2;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// The reply a case scripts for one call: the response HEADERS (<c>:status</c> first), the DATA frames' payloads exactly
/// as they are split on the wire, and the Trailers; or, for a Trailers-Only response, one HEADERS frame ending the stream.
/// </summary>
/// <param name="Headers">The response header list, <c>:status</c> included; a Trailers-Only reply sends it with <paramref name="Trailers" /> in one HEADERS frame.</param>
/// <param name="DataFrames">Each DATA frame's payload, in order; a message may span frames or share one.</param>
/// <param name="Trailers">The trailer list, sent in a HEADERS frame ending the stream; when empty, an empty DATA frame ends it instead.</param>
/// <param name="IsTrailersOnly">Whether the reply is Trailers-Only: no DATA and one HEADERS frame ending the stream.</param>
public sealed record GrpcReferenceReply(
    IReadOnlyList<HeaderField> Headers,
    IReadOnlyList<byte[]> DataFrames,
    IReadOnlyList<HeaderField> Trailers,
    bool IsTrailersOnly = false)
{
    /// <summary>The headers of a successful response: <c>:status 200</c> and <c>content-type application/grpc</c>.</summary>
    public static readonly IReadOnlyList<HeaderField> OkHeaders = [new(":status", "200"), new("content-type", "application/grpc")];

    /// <summary>A successful reply: each message in a DATA frame of its own, then <c>grpc-status 0</c>.</summary>
    /// <param name="messages">The response messages' bytes.</param>
    /// <returns>The reply.</returns>
    public static GrpcReferenceReply Ok(params IReadOnlyList<byte[]> messages) =>
        new(OkHeaders, [.. messages.Select(static message => GrpcLengthPrefixedMessage.Frame(message))], [new("grpc-status", "0")]);

    /// <summary>A Trailers-Only reply: <c>:status 200</c>, <c>content-type application/grpc</c>, <c>grpc-status</c> and, when given, <c>grpc-message</c>.</summary>
    /// <param name="status">The gRPC status code.</param>
    /// <param name="message">The <c>grpc-message</c> value, already percent-encoded; <see langword="null" /> for none.</param>
    /// <returns>The reply.</returns>
    public static GrpcReferenceReply TrailersOnly(int status, string? message = null) =>
        new(OkHeaders, [], message is null ? [new("grpc-status", $"{status}")] : [new("grpc-status", $"{status}"), new("grpc-message", message)], IsTrailersOnly: true);
}

using Curl.Http2;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>One call curl made on a stream of <see cref="GrpcReferenceServer" />: its decoded request headers and its DATA frames.</summary>
public sealed class GrpcReferenceCall
{
    private readonly List<byte[]> dataFrames = [];

    internal GrpcReferenceCall(int streamId, IReadOnlyList<HeaderField> headers)
    {
        StreamId = streamId;
        Headers = headers;
    }

    /// <summary>Gets the HTTP/2 stream the call is on.</summary>
    public int StreamId { get; }

    /// <summary>Gets the request's header list as HPACK decoded it, in the order it was sent, pseudo-headers included.</summary>
    public IReadOnlyList<HeaderField> Headers { get; }

    /// <summary>Gets each DATA frame's payload, padding removed, in the order it arrived.</summary>
    public IReadOnlyList<byte[]> DataFrames => [.. dataFrames];

    /// <summary>Gets a value indicating whether the client has ended its half of the stream.</summary>
    public bool IsRequestEnded { get; private set; }

    /// <summary>Gets the request's <c>:path</c>; empty when it sent none.</summary>
    public string Path => Header(":path") ?? string.Empty;

    /// <summary>Gets the Length-Prefixed-Messages of the DATA frames received so far.</summary>
    /// <exception cref="InvalidDataException">The DATA so far ends inside a message.</exception>
    public IReadOnlyList<GrpcReferenceMessage> Messages => GrpcLengthPrefixedMessage.Parse([.. dataFrames.SelectMany(static frame => frame)]);

    internal bool IsAnswered { get; set; }

    /// <summary>Gets the value of the first request header named <paramref name="name" />.</summary>
    /// <param name="name">The header name, lower case.</param>
    /// <returns>Its value; <see langword="null" /> when the request has no such header.</returns>
    public string? Header(string name)
    {
        foreach (HeaderField header in Headers)
        {
            if (header.Name == name)
            {
                return header.Value;
            }
        }

        return null;
    }

    internal void AddData(ReadOnlyMemory<byte> payload) => dataFrames.Add(payload.ToArray());

    internal void EndRequest() => IsRequestEnded = true;
}

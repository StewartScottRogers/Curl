namespace Curl.Extension.GrpcWeb;

/// <summary>What <see cref="GrpcWebFraming.Parse" /> read from a gRPC-Web response body.</summary>
/// <param name="Messages">The data frames' messages, in the order they came.</param>
/// <param name="Trailers">The trailers frame's fields, or <see langword="null" /> when the body had none (a trailers-only response).</param>
internal sealed record GrpcWebReply(IReadOnlyList<ReadOnlyMemory<byte>> Messages, IReadOnlyList<KeyValuePair<string, string>>? Trailers);

using Curl.Extension.Protocol.Grpc.Wire;

namespace Curl.Extension.Protocol.Grpc.Client;

/// <summary>What a call's response carried: its messages, in order, and the status that ended it.</summary>
/// <param name="Messages">The response messages.</param>
/// <param name="Status">The status from the trailers, a Trailers-Only response or an HTTP status other than 200.</param>
internal sealed record GrpcCallResult(IReadOnlyList<GrpcMessage> Messages, GrpcStatus Status);

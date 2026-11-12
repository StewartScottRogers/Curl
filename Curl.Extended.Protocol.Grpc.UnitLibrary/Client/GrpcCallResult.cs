using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>What a call's response carried: its messages, in order, and the status that ended it.</summary>
/// <param name="Messages">The response messages.</param>
/// <param name="Status">The status from the trailers, a Trailers-Only response or an HTTP status other than 200.</param>
/// <param name="Report">What <c>-w</c> reports about the response head, an empty one when none was read.</param>
internal sealed record GrpcCallResult(IReadOnlyList<GrpcMessage> Messages, GrpcStatus Status, TransferReport Report);

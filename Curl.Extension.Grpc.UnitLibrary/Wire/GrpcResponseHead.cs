namespace Curl.Extension.Grpc.Wire;

/// <summary>
/// What the first HEADERS of a gRPC response says: the HTTP status and, when the call already ended
/// there, its status.
/// </summary>
/// <param name="HttpStatus">The <c>:status</c>.</param>
/// <param name="IsTrailersOnly">Whether the headers were a Trailers-Only response, carrying <c>grpc-status</c> with no messages to follow.</param>
/// <param name="FinalStatus">The call's status when it ended with these headers (Trailers-Only, or an HTTP status other than 200 mapped as http-grpc-status-mapping.md does), otherwise <see langword="null" />: messages and trailers follow.</param>
internal sealed record GrpcResponseHead(int HttpStatus, bool IsTrailersOnly, GrpcStatus? FinalStatus);

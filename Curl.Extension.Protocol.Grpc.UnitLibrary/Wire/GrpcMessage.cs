namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>
/// One Length-Prefixed-Message of PROTOCOL-HTTP2.md, its prefix taken off.
/// </summary>
/// <param name="IsCompressed">Whether the Compressed-Flag was 1, so the payload is in the call's <c>grpc-encoding</c>.</param>
/// <param name="Payload">The message bytes.</param>
internal sealed record GrpcMessage(bool IsCompressed, byte[] Payload);

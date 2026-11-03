namespace Curl.Conformance.Extended.Grpc;

/// <summary>One Length-Prefixed-Message (PROTOCOL-HTTP2.md "Length-Prefixed-Message"): its Compressed-Flag and its message bytes.</summary>
/// <param name="IsCompressed">Whether the Compressed-Flag byte is 1.</param>
/// <param name="Payload">The message bytes.</param>
public sealed record GrpcReferenceMessage(bool IsCompressed, byte[] Payload);

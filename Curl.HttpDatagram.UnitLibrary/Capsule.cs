namespace Curl.HttpDatagram;

/// <summary>
/// One capsule of the Capsule Protocol (RFC 9297 section 3.2): a type and its value.
/// </summary>
/// <param name="Type">The capsule type, at most 2^62 - 1.</param>
/// <param name="Value">The capsule value.</param>
public sealed record Capsule(ulong Type, ReadOnlyMemory<byte> Value);

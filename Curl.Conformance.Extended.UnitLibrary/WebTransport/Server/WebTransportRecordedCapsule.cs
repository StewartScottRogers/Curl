namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>A capsule the client sent on the CONNECT stream (RFC 9297 section 3.2).</summary>
/// <param name="Type">The capsule type.</param>
/// <param name="Value">The capsule value.</param>
public sealed record WebTransportRecordedCapsule(long Type, byte[] Value);

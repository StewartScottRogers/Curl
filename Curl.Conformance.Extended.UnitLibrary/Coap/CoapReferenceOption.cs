namespace Curl.Conformance.Extended.Coap;

/// <summary>One CoAP option (RFC 7252 section 3.1): its number and its value bytes.</summary>
/// <param name="Number">The option number.</param>
/// <param name="Value">The option value.</param>
public sealed record CoapReferenceOption(int Number, byte[] Value);

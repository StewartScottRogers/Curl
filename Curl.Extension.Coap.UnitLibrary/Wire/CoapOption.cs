namespace Curl.Extension.Coap.Wire;

/// <summary>One option of a CoAP message: its number and its value bytes (RFC 7252 section 3.1).</summary>
/// <param name="Number">The option number, 0 to 65535.</param>
/// <param name="Value">The value bytes, at most 65804 of them.</param>
internal sealed record CoapOption(int Number, byte[] Value);

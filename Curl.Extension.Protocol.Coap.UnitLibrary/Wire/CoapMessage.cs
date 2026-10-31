namespace Curl.Extension.Protocol.Coap.Wire;

/// <summary>One CoAP message, version 1 (RFC 7252 section 3).</summary>
/// <param name="Type">The message type.</param>
/// <param name="Code">The request method or response code.</param>
/// <param name="MessageId">The 16-bit Message ID.</param>
/// <param name="Token">The token, 0 to 8 bytes.</param>
/// <param name="Options">The options; <see cref="CoapMessageEncoding.Encode"/> writes them in ascending number.</param>
/// <param name="Payload">The payload; empty when the message has none.</param>
internal sealed record CoapMessage(
    CoapMessageType Type,
    CoapCode Code,
    ushort MessageId,
    byte[] Token,
    IReadOnlyList<CoapOption> Options,
    byte[] Payload);

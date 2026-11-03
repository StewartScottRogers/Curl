namespace Curl.Conformance.Extended.Coap;

/// <summary>A datagram curl sent to a <see cref="CoapReferenceServer" />: its bytes, the message they decode to and when it arrived on the run's clock.</summary>
/// <param name="Bytes">The datagram's bytes.</param>
/// <param name="Message">The decoded message.</param>
/// <param name="ReceivedAt">The run's clock when the datagram arrived.</param>
public sealed record CoapReceivedDatagram(byte[] Bytes, CoapReferenceMessage Message, DateTimeOffset ReceivedAt);

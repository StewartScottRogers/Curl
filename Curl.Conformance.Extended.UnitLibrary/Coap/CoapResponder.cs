namespace Curl.Conformance.Extended.Coap;

/// <summary>Gives a <see cref="CoapReferenceServer" /> answer to one datagram curl sent.</summary>
/// <param name="received">The message curl sent.</param>
/// <param name="index">How many datagrams curl sent before it, from 0.</param>
/// <returns>The datagrams to send back, in order (<see cref="CoapResponses" /> builds them); none to stay silent.</returns>
public delegate IReadOnlyList<byte[]> CoapResponder(CoapReferenceMessage received, int index);

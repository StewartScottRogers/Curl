namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>An HTTP datagram the client sent (RFC 9297): its quarter stream id and its payload.</summary>
/// <param name="QuarterStreamId">The quarter stream id prefix: the session's stream id divided by four.</param>
/// <param name="Payload">The bytes after the prefix.</param>
public sealed record WebTransportRecordedDatagram(long QuarterStreamId, byte[] Payload);

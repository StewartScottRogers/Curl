# ADR-1043: QUIC DATAGRAM frames are opt-in through the transport parameters and go one per packet

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4222)

## Context

WebTransport (BL-4113) and MASQUE (BL-4144) need HTTP/3 datagrams (RFC 9297), which ride on QUIC
DATAGRAM frames (RFC 9221). curl's ngtcp2 build does not advertise `max_datagram_frame_size`, so
curl's own `--http3` must keep sending exactly the transport parameters ADR-0144 section 5 pinned.
`Curl.Quic.UnitLibrary` had no DATAGRAM frame, no parameter 0x20 and no way for a caller holding the
connection to send or receive one.

## Decision

- `QuicTransportParameters.MaxDatagramFrameSize` holds parameter 0x20. Its default is 0, RFC 9221's
  "accepts none", and 0 is never written, so `CurlClientDefaults` encodes byte for byte as before.
  A caller opts in by giving `QuicClientSettings.TransportParameters` a non-zero value; no separate
  setting duplicates it. It is written after `preferred_address` and before `version_information`.
- `QuicDatagramFrame` is types 0x30 (to the end of the packet) and 0x31 (with a Length), allowed in
  0-RTT and 1-RTT packets. The codec reads either; the client always sends 0x31.
- A DATAGRAM frame received when the client advertised 0, or larger than it advertised, closes the
  connection with PROTOCOL_VIOLATION. Sending is refused (`InvalidOperationException`) until the
  server has advertised a value larger than the frame's three-byte overhead.
- A DATAGRAM frame is ack-eliciting and is never queued again when its packet is lost or probed.
- Each queued datagram is put in a packet of its own, so two never overflow one packet, and it waits
  for the congestion window like any other data (RFC 9221 section 5.4). One datagram carries at most
  1100 application bytes whatever the server allows: what a 1200-byte datagram leaves after the
  longest short header, the AEAD tag, the frame's type and length, and room for an ACK and control
  frames sharing the packet.
- The caller reaches datagrams through `IDatagramCarryingConnection` in
  `Curl.Protocol.Abstractions.UnitLibrary`, an optional interface `QuicConnection` implements
  beside `IMultiplexedConnection`, so no other implementation of that interface changes.

## Consequences

- curl's HTTP/3 is unchanged on the wire.
- A wrapper around `IMultiplexedConnection` that does not pass the interface through hides it from
  an extension; Engine's `SocketCallbackMultiplexedConnection` does, and a task carries that.
- Received datagrams queue without a bound until taken; a caller that never reads them should not
  advertise the parameter.

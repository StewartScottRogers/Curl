# ADR-1162: The WireGuard extension carries UDP, and so QUIC, through the tunnel

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4355
- Decided by Claude under Stewart's delegation

## Context

ADR-1159 carried TCP alone: under `--wireguard <conf>` the hook's datagram connector refused every UDP socket with
exit 4, so `--http3` and `--http3-only` could not run and no QUIC left outside the tunnel. HTTP/3 reaches UDP two ways
in Curl: the hook's `IDatagramConnector` (`DatagramConnectorOver`), which the engine's QUIC dialer opens its channels
through on the real network (BL-4318), and `IConnector.ConnectMultiplexedAsync`, which the HTTP handler calls on the
connector it is given - here `WireGuardConnector`, which replaces curl's.

## Decision

- The IP stack gains UDP (RFC 768): `IpStack/Udp/UdpDatagramCodec` builds and reads the 8-byte header with the RFC 1071
  checksum over the pseudo-header; a computed zero is sent as all ones, and a received zero checksum is accepted over
  IPv4 and refused over IPv6 (RFC 8200 section 8.1). `UserSpaceUdpChannel` is an `IDatagramChannel` over an
  `IIpPacketChannel`, from a random ephemeral port (49152 to 65535), keeping only sound UDP datagrams addressed to its
  own address and port and truncating one longer than the caller's buffer, as a socket does.
- `WireGuardDatagramConnector` opens each channel inside its own tunnel, as `WireGuardConnector` opens each TCP
  connection: cryptokey routing picks the peer and source address, a new UDP socket and handshake to it, then the
  channel over `WireGuardPacketTunnel`; closing the channel closes the socket and zeroes the keys
  (`WireGuardTunnelLifetime`). A host name is resolved by `WireGuardConnector`'s DNS over TCP through the tunnel
  (ADR-1161); none is exit 6, an address no peer routes or a failed socket or handshake exit 7.
- `WireGuardConnector.ConnectMultiplexedAsync` and `ConnectMultiplexedSessionAsync` hand QUIC to curl's connector,
  which the hook now passes in: curl's QUIC dialer then opens its UDP through the hook's datagram connector, so its
  datagrams travel sealed as transport data. This keeps one QUIC client, the engine's, rather than a second one in
  the extension, which may reference no `Curl.Quic` or `Curl.Networking` library (ADR-0999).

## Consequences

- `curl --wireguard wg0.conf --http3-only https://10.0.0.1/` runs QUIC through the tunnel. A host name given to an
  HTTP/3 transfer is resolved by curl's QUIC dialer through curl's resolver before the datagram connector sees an
  address, so its lookup does not go through the tunnel; an address in the URL is unaffected.
- Each QUIC connection is its own tunnel and handshake, as each TCP connection is.

# ADR-1159: The WireGuard extension tunnels each connection through a user-space WireGuard peer

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4147
- Decided by Claude under Stewart's delegation

## Context

Stewart chose a WireGuard extension (ADR-0999): `curl --wireguard <conf>` runs a transfer through a WireGuard
tunnel with no system interface. Its pieces landed first: the wg-quick configuration parser (ADR-1117, BL-4326),
the user-space IPv4, IPv6 and TCP client (ADR-1121, BL-4324) and the Noise IK handshake and transport data
(ADR-1136, BL-4323). This decision joins them under the connection hooks (ADR-1010). Upstream curl has no
WireGuard support, so the choices follow the WireGuard whitepaper (Donenfeld, revision e2da747 of 2020-06-01,
"WireGuard: Next Generation Kernel Network Tunnel") and wg-quick(8), not curl.

## Decision

- **Option.** `--wireguard <conf>`, one value: the path of a wg-quick configuration file. It is an
  `IConnectionHook` (ADR-1010), so it is refused as an unknown option (exit 2) with `CURL_EXTENSIONS=0` and
  cannot be combined with another connection hook (`--npipe`, `--vm-socket`). A file that cannot be read, is not a
  wg-quick file, or has no `[Peer]` with an `Endpoint` is a command-line error (exit 2) naming the file.
- **One tunnel per connection.** Each connection the transfer opens gets its own UDP socket, handshake and
  transport session, and is closed with them. A transfer opens few connections, and this keeps one TCP
  connection on each `IIpPacketChannel`, as the IP stack expects, with no demultiplexing by port.
- **Cryptokey routing** (whitepaper section 2): the target address goes to the peer with an `Endpoint` whose
  `AllowedIPs` hold it with the longest prefix; the source is the first interface `Address` of the target's family.
  No route, or no address of that family, fails with exit 7 before any packet is sent.
- **Handshake timing** (sections 6.1 and 6.4): a new initiation every Rekey-Timeout (5 s) until a response, giving
  up after Rekey-Attempt-Time (90 s, 18 initiations) with exit 7. The whitepaper's up to 333 ms of jitter is left
  out: it spreads the rekeys of many concurrent sessions, and one transfer has one at a time.
- **MTU.** wg-quick's 1420 when the configuration names none; the SYN's MSS is the MTU less 40 bytes (IPv4) or
  60 (IPv6).
- **TLS.** Curl's own TLS provider runs over the tunnel's TCP connection (ADR-1055), offering curl's ALPN list
  for `https`, as the named pipe and vsock extensions do.
- **UDP is refused, not leaked.** The IP stack carries TCP alone, so the hook's datagram connector refuses every
  UDP socket with exit 4 rather than let QUIC leave outside the tunnel. BL-4355 adds UDP inside the tunnel.
- **Names.** Only IP address targets are tunnelled for now; a host name fails with exit 6 rather than be looked
  up outside the tunnel. ADR-1161 (BL-4354) since resolves names by DNS over TCP to the configuration's `DNS` servers inside the tunnel.
- **The peer's endpoint** is resolved with the system resolver and reached with a system UDP socket
  (`SystemWireGuardUdpOpener`, excluded from coverage under ADR-0083 as a thin socket adapter); everything above
  it is tested against a whitepaper-written peer with no network.

## Not yet built, with why

- Rekeying an established session (section 6.2) and keepalives (section 6.5): a session lives one connection,
  and `WireGuardTransportSession` already refuses to seal past Reject-After-Time, which ends the connection with
  an error rather than send under expired keys. Long transfers past 180 s need rekeying, which BL-4356 adds.
- `ListenPort` and wg-quick's host-side keys are read but not used: an initiator on an
  ephemeral port needs neither. `PersistentKeepalive` is BL-4356's, with the keepalives.

## Consequences

`curl --wireguard wg0.conf http://10.0.0.1/` runs through a WireGuard peer with no administrator rights. Name
resolution and UDP through the tunnel follow in BL-4354 and BL-4355.

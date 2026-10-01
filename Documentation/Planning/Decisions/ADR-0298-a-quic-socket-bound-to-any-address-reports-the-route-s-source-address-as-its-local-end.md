# ADR-0298 — A QUIC socket bound to any address reports the route's source address as its local end

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1051.

## Context

curl.se's 8.18.0 ngtcp2 build, `-v --http3-only https://cloudflare-quic.com/` (measured by
BL-734), writes `Established connection to cloudflare-quic.com (104.18.26.14 port 443) from
192.168.1.174 port 51486`. curl's `cf_socket` connects its UDP socket to the peer, so
`getsockname` returns the source address the kernel's route picked. Curl's
`UdpDatagramChannel` binds to the any address and never connects, so `LocalEndPoint` was
`0.0.0.0` and `-v` and `%{local_ip}` said so.

Connecting the channel's own socket would match curl most literally, but the same channel
carries TFTP, whose server answers from a new port (RFC 1350 section 4) that a connected
socket would drop, and macOS refuses `sendto` with a destination on a connected socket
(`EISCONN`).

## Decision

When the channel's socket is bound to the any address, `UdpDatagramChannel.LocalEndPoint`
reports the bound port with the address a throwaway UDP socket gets once connected to the
server - connecting a UDP socket sends nothing, it only asks the route. With no route
(`SocketException`) the any address stays. A socket bound to a given address
(`--interface`) reports that address unchanged. The channel's own socket stays unconnected.

## Consequences

- `-v`'s `Established connection ... from <ip> port <port>` and `%{local_ip}` for a QUIC
  connect name the interface address, as curl's do.
- One extra socket is opened and closed each time `LocalEndPoint` is read on a wildcard-bound
  channel; QUIC reads it once per connection.

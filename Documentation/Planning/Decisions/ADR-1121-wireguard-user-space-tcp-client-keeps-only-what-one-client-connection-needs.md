# ADR-1121 — The WireGuard extension's user-space TCP client keeps only what one client connection needs

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4324.

## Context

BL-4324 gives `Curl.Extension.WireGuard.UnitLibrary` its own IPv4, IPv6 and TCP client (`IpStack/`), because the
tunnel carries raw IP packets and the transfer above needs a byte stream. The stack opens one connection, from a
client, to a server inside the tunnel. RFC 791, RFC 8200 and RFC 9293 describe far more than that needs, and the
task leaves some of it open (fragment reassembly, UDP).

## Decision

- **No fragmentation.** Packets sent are never fragmented: IPv4 ones carry Don't Fragment, and the MSS the SYN offers
  is the tunnel's MTU less the headers, so no segment needs it. Received fragments (More Fragments set or a non-zero
  offset) are dropped and not reassembled: TCP peers do path MTU discovery from the MSS, so fragments are not
  expected inside a WireGuard tunnel.
- **What is dropped.** Packets that are not IPv4 or IPv6, are truncated, have a wrong IPv4 header checksum or TCP
  checksum, carry another protocol, or come from another address or port are dropped silently. IPv4 options are
  skipped by the header length. IPv6 extension headers are not followed, so a packet whose Next Header is not TCP is
  dropped.
- **Receiving.** The receive buffer is 65535 bytes and is the window advertised (no window scaling). Only the segment
  at the next expected sequence number is taken; one that arrives out of order is dropped and the last in-order byte
  is acknowledged again, so the peer retransmits it. Data beyond the free space is cut off, and a FIN is taken only
  with all of its segment's data. When reading frees half the buffer after the window fell under half, an ACK
  updates the window.
- **Sending.** Segments are no larger than the peer's MSS (536 when its SYN-ACK has none) and stay within its window.
  There is no Nagle delay, no delayed ACK, no congestion control and no persist timer: one client connection across
  a tunnel needs none of them to be correct.
- **Retransmission.** RFC 6298 on the injected `TimeProvider`: 1 s before the first sample, then SRTT + max(G, 4 ×
  RTTVAR) with G = 1 ms, clamped to 1 s to 60 s, doubled on each timeout; Karn's algorithm drops samples of
  retransmitted segments. Only the earliest unacknowledged segment is retransmitted. A SYN is retransmitted 6 times
  and a segment 15 times, as Linux's `tcp_syn_retries` and `tcp_retries2` defaults, before the connection fails with
  `SocketError.TimedOut`.
- **Reset and close.** A RST in SYN-SENT is taken when it acknowledges the SYN; once connected, only when its
  sequence number is the next expected one (RFC 5961 section 3.2's exact match; no challenge ACK). It fails the
  connection with `SocketError.ConnectionReset`, which reads and writes then throw inside an `IOException`.
  `DisposeAsync` sends FIN and waits until everything is acknowledged or the connection fails; it does not wait for
  the peer's FIN and keeps no TIME-WAIT, since the client's port is random and used once.
- **UDP is left to the task that needs it.** The configuration's `DNS` server is not used yet; BL-4147 decides how
  names are resolved inside the tunnel.
- **Asynchronous only.** The stream throws `NotSupportedException` from synchronous `Read` and `Write`; synchronous
  `Dispose` aborts without FIN, since a FIN handshake cannot finish without blocking.

## Consequences

A lossy or reordering tunnel costs retransmissions rather than buffered segments, which is correct but slower. Each
item left out above can be added in `IpStack/` without changing its public types.

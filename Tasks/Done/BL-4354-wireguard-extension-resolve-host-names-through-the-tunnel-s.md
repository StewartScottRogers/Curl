---
id: BL-4354
title: WireGuard extension: resolve host names through the tunnel's DNS servers
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147]
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel, Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1159-wireguard-extension-tunnels-each-connection-through-a-user-space-wireguard-peer.md]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4354 — WireGuard extension: resolve host names through the tunnel's DNS servers

## Goal

`curl --wireguard wg0.conf http://name/` resolves `name` through the configuration's `DNS` servers inside the tunnel, never through the system resolver.

## Context

BL-4147 (ADR-1159) tunnels connections to IP addresses only: `WireGuardConnector` fails a host name with exit 6 rather than leak the lookup outside the tunnel. The IP stack carries TCP alone, so the natural first step is DNS over TCP (RFC 7766, RFC 1035 section 4.2.2) to the first `DNS` address of the `[Interface]` through the same tunnel; a name with no `DNS` line stays exit 6. Tested against RFC 1035 and RFC 7766 with `TunnelTestPeer`, no network.

## Acceptance criteria

- [x] A host name target is resolved (A for an IPv4 interface address, AAAA for IPv6) by a DNS over TCP query to the configuration's first DNS server through the tunnel, and the connection then opens to the answer.
- [x] Without a DNS line, or when the answer has no address, the transfer fails with exit 6 and nothing is sent outside the tunnel; unit tests.
- [x] ADR-1159 or a new ADR records the choice; the four quality gates hold by Measure-CodeQuality.ps1.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan (ADR-1161): `WireGuardConnector` resolves a non-IP host by one DNS over TCP query (RFC 1035 4.2.2, RFC 7766)
  to the first `DNS` entry that is an IP address, over its own tunnel (handshake and TCP to port 53, closed once the
  answer is read), then connects to the first address of the answer as for an IP target. A when the interface has an
  IPv4 address, else AAAA; CNAMEs in the answer skipped; IDN names sent as A-labels. Every failure is exit 6
  `Could not resolve host: <name>`; the system resolver is never asked. New `Tunnel/WireGuardDnsMessage.cs`.
- Default taken: a separate tunnel for the lookup rather than sharing one session between two TCP connections, as the
  IP stack has one receive loop per channel; costs a second handshake per connection.
- Touches widened to the library's CLAUDE.md (it said "IP address targets only (BL-4354 adds names)") and ADR-1159's
  Names line (now points at ADR-1161); no other task in Doing on origin/work/dark-factory names WireGuard.
- `TunnelTestPeer.AcceptHandshakeAsync` now skips transport datagrams the DNS tunnel sent last before the next
  initiation.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary: 100% line, 100% branch, 0 failing,
  worst CRAP 10. WireGuard tests 280 passed; full fast run green; build 0 warnings. Tests are platform-neutral (no
  paths, OS errors or certificates; every concurrent step waits on the peer's channel).

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e007d461 91e0739a 8b7b522d 3d836d5e.
- 2026-10-30: Doing -> Done. --wireguard resolves host names by DNS over TCP to the configuration's first DNS server through the tunnel (ADR-1161)

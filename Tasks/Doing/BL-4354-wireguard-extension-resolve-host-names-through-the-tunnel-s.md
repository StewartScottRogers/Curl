---
id: BL-4354
title: WireGuard extension: resolve host names through the tunnel's DNS servers
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147]
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel, Curl.Extension.WireGuard.UnitTests/Tunnel]
requirement: none
created: 2026-10-30
completed:
---
# BL-4354 — WireGuard extension: resolve host names through the tunnel's DNS servers

## Goal

`curl --wireguard wg0.conf http://name/` resolves `name` through the configuration's `DNS` servers inside the tunnel, never through the system resolver.

## Context

BL-4147 (ADR-1159) tunnels connections to IP addresses only: `WireGuardConnector` fails a host name with exit 6 rather than leak the lookup outside the tunnel. The IP stack carries TCP alone, so the natural first step is DNS over TCP (RFC 7766, RFC 1035 section 4.2.2) to the first `DNS` address of the `[Interface]` through the same tunnel; a name with no `DNS` line stays exit 6. Tested against RFC 1035 and RFC 7766 with `TunnelTestPeer`, no network.

## Acceptance criteria

- [ ] A host name target is resolved (A for an IPv4 interface address, AAAA for IPv6) by a DNS over TCP query to the configuration's first DNS server through the tunnel, and the connection then opens to the answer.
- [ ] Without a DNS line, or when the answer has no address, the transfer fails with exit 6 and nothing is sent outside the tunnel; unit tests.
- [ ] ADR-1159 or a new ADR records the choice; the four quality gates hold by Measure-CodeQuality.ps1.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e007d461 91e0739a 8b7b522d 3d836d5e.

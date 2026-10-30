---
id: BL-4371
title: WireGuard extension: carry UDP datagrams inside the tunnel (RFC 768 codec and datagram connector)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354, BL-4357]
touches: [Curl.Extension.WireGuard.UnitLibrary/IpStack/Udp, Curl.Extension.WireGuard.UnitTests/IpStack/Udp, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardDatagramConnector.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardTunnelLifetime.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs, Curl.Extension.WireGuard.UnitLibrary/WireGuardConnectionHook.cs, Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md, Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Extension.WireGuard.UnitTests/WireGuardConnectionHookTests.cs]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4371 — WireGuard extension: carry UDP datagrams inside the tunnel (RFC 768 codec and datagram connector)

## Goal

The WireGuard extension opens a UDP channel inside the tunnel, so no datagram leaves outside it.

## Context

BL-4147 (ADR-1159) carries TCP alone and `WireGuardDatagramConnector` refuses every UDP socket (exit 4). This adds UDP (RFC 768, with the RFC 1071 checksum `InternetChecksum` already has) to the IP stack and a datagram connector that opens an `IDatagramChannel` through the tunnel. Split from BL-4355 (first half).

## Acceptance criteria

- [x] The hook's datagram connector opens a UDP channel inside the tunnel whose datagrams are sealed as transport data and checked against RFC 768 by unit tests with recorded bytes.
- [x] `Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md` states that UDP and QUIC are carried, no longer "TCP only".
- [x] The four quality gates hold by Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4355, which went over its cost cap.
- Continues the stashed work of BL-4355.
- The stash holds this work done in the working tree: `IpStack/Udp/UdpDatagramCodec` (RFC 768, checksum over the pseudo-header, zero sent as all ones, zero accepted over IPv4 and refused over IPv6), `UdpDatagram`, `UserSpaceUdpChannel` (an `IDatagramChannel` over `IIpPacketChannel`); `WireGuardDatagramConnector` opens each channel in its own tunnel (`WireGuardTunnelLifetime` closes socket and keys), host names resolved by `WireGuardConnector.ResolveThroughTunnelAsync`; the hook gives it. Recorded datagrams: "hi" 10.0.0.2:50000 -> 10.0.0.1:443 checksum 0xBE62, [fd00::2] -> [fd00::1] 0xD860. WireGuard tests were 308 green. ADR-1163 was written (uncommitted, with the code). Also decided in ADR-1163: `WireGuardConnector.ConnectMultiplexedAsync` hands QUIC to curl's connector (the hook passes it), whose QUIC dialer opens UDP through the hook's datagram connector (BL-4318); unit tests in `WireGuardConnectionHookTests`.
- Second run: dropped the stash's unused test references to Curl.Console and Curl.Conformance.Http3Server (and Console's InternalsVisibleTo), leftovers of the HTTP/3 end-to-end test, which is BL-4372's; updated the CLAUDE.md; Measure-CodeQuality.ps1 found `WireGuardDatagramConnector.OpenToAsync`'s rethrow after an await in a catch uncovered, so it now takes the failure out of the catch as `WireGuardConnector` does, and a handshake timeout test covers the `TimeoutException` branch. Quality: 100% line and branch, 256 members, worst CRAP 10. Fast tests: 111 projects green, WireGuard 309. CI on Linux and macOS: the change is platform-neutral (no paths, OS errors or certificates) and the fast tests are green here.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 954a2d25 d609389f 866d5a6d 3d836d5e.
- 2026-10-30: Doing -> Done. UDP and QUIC carried inside the tunnel; gates and fast tests green

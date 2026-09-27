---
id: BL-356
title: Cover TcpDialer.DialAsync and UdpDatagramChannel send and receive in the fast run
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-356 — Cover TcpDialer.DialAsync and UdpDatagramChannel send and receive in the fast run

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` (fast run, Integration excluded) reports no failing member in `TcpDialer` or `UdpDatagramChannel`.

## Context

- Found by BL-354 on 2026-09-27. The fast-run audit reports `TcpDialer.DialAsync` at 14.29% line coverage and `UdpDatagramChannel.SendAsync` / `ReceiveAsync` at 50% / 60%: they open real sockets, so they are only reached by the loopback tests in `TcpDialerTests` and `UdpDatagramChannelTests`, which carry `TestCategory("Integration")`.
- The 100% quality gate is measured on the fast run. Either put a seam between these members and `Socket` so a fake can drive them without the network, or record in an ADR why these thin socket adapters are measured by the Integration run instead, and make the audit say so.
- `SslStreamTlsProvider.CreateCipherSuitesPolicy` is the fourth gap in the same audit and is BL-268's, not this task's.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` lists no failing member in `TcpDialer.cs` or `UdpDatagramChannel.cs`, or an ADR states why they are measured by the Integration run and the script excludes them on that ADR's authority.
- [ ] `dotnet test --filter "TestCategory!=Integration"` stays off the network.

## Notes

## Log

- 2026-09-27: Created.

---
id: BL-357
title: Cover TcpDialer.DialAsync and UdpDatagramChannel send and receive in the fast run
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0083-thin-socket-adapters-are-measured-by-the-integration-run.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-357 — Cover TcpDialer.DialAsync and UdpDatagramChannel send and receive in the fast run

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` (fast run, Integration excluded) reports no failing member in `TcpDialer` or `UdpDatagramChannel`.

## Context

- Found by BL-354 on 2026-09-27. The fast-run audit reports `TcpDialer.DialAsync` at 14.29% line coverage and `UdpDatagramChannel.SendAsync` / `ReceiveAsync` at 50% / 60%: they open real sockets, so they are only reached by the loopback tests in `TcpDialerTests` and `UdpDatagramChannelTests`, which carry `TestCategory("Integration")`.
- The 100% quality gate is measured on the fast run. Either put a seam between these members and `Socket` so a fake can drive them without the network, or record in an ADR why these thin socket adapters are measured by the Integration run instead, and make the audit say so.
- `SslStreamTlsProvider.CreateCipherSuitesPolicy` is the fourth gap in the same audit and is BL-268's, not this task's.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` lists no failing member in `TcpDialer.cs` or `UdpDatagramChannel.cs`, or an ADR states why they are measured by the Integration run and the script excludes them on that ADR's authority.
- [x] `dotnet test --filter "TestCategory!=Integration"` stays off the network.

## Notes

- Decision (ADR-0083, decided by Claude under Stewart's delegation): the three members are
  thin socket adapters, so they carry `[ExcludeFromCodeCoverage(Justification = "ADR-0083: ...")]`
  and stay measured by their Integration loopback tests. A `Socket` seam was rejected because
  its default implementation is the same socket call, so the gap would only move into a
  lambda; loopback round trips in the fast run were rejected because unit tests need no
  network (Product-Overview). The script already honours the attribute and lists each
  exclusion, so it needed no change.
- Pipeline `feature` was run in-session: the change is three attributes, an ADR and docs, no
  behaviour change and no new tests.
- `touches` widened to the new ADR file and `Documentation/Planning/Decisions/README.md`
  (its index row); no task in Doing names either.
- Result: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line,
  100% branch, 270 members, 0 failing, with the three exclusions listed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Networking library passes the fast-run quality gate; the three socket adapters are excluded on ADR-0083's authority and measured by the Integration run

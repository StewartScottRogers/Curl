---
id: BL-1154
title: Make UdpChannelOpener.OpenFrom's bind callback covered on every fast run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1154 — Make UdpChannelOpener.OpenFrom's bind callback covered on every fast run

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports `UdpChannelOpener.OpenFrom` at 100% line coverage on every run, not only on some.

## Context

- Found in BL-964 (2026-10-02): of three consecutive `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` runs on the same tree, one reported `UdpChannelOpener.OpenFrom` (`UdpChannelOpener.cs:45`) at 85.71% line coverage, line 53 uncovered: the lambda `(socket, firstLocalEndPoint) => TcpDialer.BindLocalEnd(...)`. The next run reported 100%.
- The lambda runs only when a fast test actually opens the `UdpDatagramChannel` it is given to; find that test and why it sometimes does not reach the bind (a skipped condition, an ordering or a race), and make it deterministic, or mark the lambda `[ExcludeFromCodeCoverage]` under ADR-0083 if it only runs against a real socket.

## Acceptance criteria

- [ ] Five consecutive `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` runs each report `UdpChannelOpener.OpenFrom` at 100% line and branch coverage.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.

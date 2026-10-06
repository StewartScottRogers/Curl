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
completed: 2026-10-02
---
# BL-1154 — Make UdpChannelOpener.OpenFrom's bind callback covered on every fast run

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports `UdpChannelOpener.OpenFrom` at 100% line coverage on every run, not only on some.

## Context

- Found in BL-964 (2026-10-02): of three consecutive `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` runs on the same tree, one reported `UdpChannelOpener.OpenFrom` (`UdpChannelOpener.cs:45`) at 85.71% line coverage, line 53 uncovered: the lambda `(socket, firstLocalEndPoint) => TcpDialer.BindLocalEnd(...)`. The next run reported 100%.
- The lambda runs only when a fast test actually opens the `UdpDatagramChannel` it is given to; find that test and why it sometimes does not reach the bind (a skipped condition, an ordering or a race), and make it deterministic, or mark the lambda `[ExcludeFromCodeCoverage]` under ADR-0083 if it only runs against a real socket.

## Acceptance criteria

- [x] Five consecutive `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` runs each report `UdpChannelOpener.OpenFrom` at 100% line and branch coverage.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Cause: the uncovered line in BL-964's run was OpenFrom's closing brace (line 53 before BL-1078, 54 now), not the lambda. The lambda runs on every OpenFrom call, but the brace runs only when OpenFrom returns. The only fast tests that returned (`BindsTheFirstFreePortOfTheRange`, `WritesEachBusyPortThenTheLocalPortBound`) bind the port after a taken one and go inconclusive when another process, such as a parallel lane, holds it. Every other OpenFrom test throws.
- Reproduced: with those two tests filtered out, OpenFrom measured 87.5% with line 54 at 0 hits.
- Fix: `UdpChannelOpener_OpenFrom_WithPortZero_BindsTheLocalAddressOnAnEphemeralPort` binds loopback port 0, which always succeeds. With the two range tests filtered out, OpenFrom now measures 100%. No `[ExcludeFromCodeCoverage]` needed.
- Gates: `dotnet build Curl.slnx -warnaserror` clean. Five consecutive `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` runs each reported 0 failing members, and all fast tests passed on each run. In run 3, `WritesEachBusyPortThenTheLocalPortBound` was skipped (the flake's trigger) and OpenFrom stayed at 100%.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. UdpChannelOpener.OpenFrom measures 100% on every run: a port-0 test always reaches its return

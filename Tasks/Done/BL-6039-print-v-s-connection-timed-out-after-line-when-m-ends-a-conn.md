---
id: BL-6039
title: Print -v's Connection timed out after line when -m ends a connect on Linux
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitTests/TcpConnectorTimedOutLineTests.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6039 — Print -v's Connection timed out after line when -m ends a connect on Linux

## Goal

On Linux, `curl -sv -m 3 http://192.0.2.20:1/` prints `* Connection timed out after N milliseconds` between `Trying` and `closing connection #0`, exit 28, as curl 8.18.0 does.

## Context

Seen in BL-6015 on 2026-11-26 with a linux-x64 self-contained publish of Curl.Console run in WSL Ubuntu: exit 28, but no timed out line, with and without `--doh-url`. Ubuntu curl 8.18.0 prints it, and so does Curl built for Windows. ADR-0117 says `TcpConnector` reports the line; the likely start is a race between the `-m` limit and the connect limit.

## Acceptance criteria

- [x] A unit test pins that a connect ended by the `-m` limit reports `Connection timed out after N milliseconds`, whichever of the two limits fires first.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `-m`'s clock starts before the connect's, so on Linux its cancellation reached `TcpConnector` before the connector's own limit had passed, and escaped without the line. Windows' timer granularity hid it.
- Decision (ADR-1453): the connect limit passed when the connector's own timer fired, or the clock is within a quarter of the limit (500 ms at most) of it; the reported N is never below the limit. Covers resolving too.
- `touches` gained `Curl.Networking.UnitLibrary/CLAUDE.md`, whose sentence on cancellations the change made untrue; no task in Doing named it.
- Verified in WSL Ubuntu with a linux-x64 self-contained publish (before the N-never-below-the-limit step, which the unit tests pin): `-sSv -m 3 http://192.0.2.20:1/` printed the line three runs of three, exit 28, at 2855, 3009 and 2961 ms; `-m 0.3`, `--connect-timeout 1` and `dict://` with `-m 1` printed it too.
- Gates: `dotnet build` 0 warnings, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members.
- Filed BL-6071 (HTTP's own N, and redirect hops) and BL-6072 (QUIC dial and UDP tunnel).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 30cb71e1 3d836d5e.
- 2026-11-26: Doing -> Done. -m ending a TCP connect or resolve prints -v's timed out line on Linux too (ADR-1453)

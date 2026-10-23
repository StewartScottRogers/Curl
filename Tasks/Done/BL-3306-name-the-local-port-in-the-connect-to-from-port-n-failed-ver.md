---
id: BL-3306
title: Name the --local-port in the 'connect to ... from ... port N failed' verbose line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/AddressFamilyRace.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/BoundConnectFailedException.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3306 — Name the --local-port in the 'connect to ... from ... port N failed' verbose line

## Goal

When a connect from a --local-port fails, Curl's verbose line names that local port as curl does ("from 0.0.0.0 port 49000"), not "port 0".

## Context

Found by BL-3282. Measured: real curl -sv --local-port 49000-49010 http://127.0.0.1:9/ prints "* connect to 127.0.0.1 port 9 from 0.0.0.0 port 49000 failed: Connection refused"; Curl, when its connect from 49000 failed with Address already in use, printed "from 0.0.0.0 port 0". AddressFamilyRace.ConnectFailedLine always writes port 0 (ADR-0100) because a failed dial reports no local end point; with a bound local port it should name that port. The local-port differential case itself differs only by run order (a TIME_WAIT 4-tuple left by whichever binary ran first); both exit 0 when run alone.

## Acceptance criteria

- [x] With --local-port, a failed connect's verbose line names the bound local port as real curl does, pinned by a unit test in Curl.Networking.UnitTests.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- Touches widened to TcpDialer.cs and the new BoundConnectFailedException.cs: the race only sees the dial's exception, so the bound end has to come from the dialer. No task in Doing on origin/work/dark-factory named either file.
- Decision (sensible default, no ADR needed as it only follows measured curl): `TcpDialer.ConnectFromBoundEndAsync` rethrows a failed connect from a socket bound to a non-zero local port as `BoundConnectFailedException` (a `SocketException` with the same error code and message, plus the bound `LocalEndPoint`), and `AddressFamilyRace.ConnectFailedLine` names that end. A bind with port 0 (`--interface` alone) and an unbound dial keep ADR-0100's `port 0`, since the race has no OS-chosen port to name.
- Pinned by `TcpConnectorTests.ConnectAsync_WhenTheDialFromABoundLocalPortFails_NamesThatPortInTheFailureLine` (platform-neutral: the reason text is "Connection refused" on every platform, as the existing refused-dial test pins). Fast tests: 45 assemblies passed, 0 failed. Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 212c44be 69edf767.
- 2026-10-23: Doing -> Done. A failed connect from a --local-port names the bound port in its -v line, as curl does

---
id: BL-3302
title: Name the --local-port in the 'connect to ... from ... port N failed' verbose line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/AddressFamilyRace.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3302 — Name the --local-port in the 'connect to ... from ... port N failed' verbose line

## Goal

When a connect from a --local-port fails, Curl's verbose line names that local port as curl does ("from 0.0.0.0 port 49000"), not "port 0".

## Context

Found by BL-3282. Measured: real curl -sv --local-port 49000-49010 http://127.0.0.1:9/ prints "* connect to 127.0.0.1 port 9 from 0.0.0.0 port 49000 failed: Connection refused"; Curl, when its connect from 49000 failed with Address already in use, printed "from 0.0.0.0 port 0". AddressFamilyRace.ConnectFailedLine always writes port 0 (ADR-0100) because a failed dial reports no local end point; with a bound local port it should name that port. The local-port differential case itself differs only by run order (a TIME_WAIT 4-tuple left by whichever binary ran first); both exit 0 when run alone.

## Acceptance criteria

- [ ] With --local-port, a failed connect's verbose line names the bound local port as real curl does, pinned by a unit test in Curl.Networking.UnitTests.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.

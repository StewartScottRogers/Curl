---
id: BL-2019
title: Run GF-0004's upstream cases through the gap harness and record why Curl's authenticated retry never reaches the server
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
lane: no
requirement: none
created: 2026-10-10
completed:
---
# BL-2019 — Run GF-0004's upstream cases through the gap harness and record why Curl's authenticated retry never reaches the server

## Goal

The cause of GF-0004's remaining failures is known from the gap harness itself: what its server does on `swsclose`, and what Curl writes and exits with in test64, test153 and test2061. It is written under this task's Notes and in BL-2000's Notes, so a lane can fix it.

## Context

Interactive only: the gap harness (`Gap/Tools/Measure-UpstreamCases.cs`) and the upstream `tests/data` cases under `%LOCALAPPDATA%\Curl\gap\upstream\8.21.0` are audit-guarded, and lanes may not read them (guard-audit-paths.ps1, ADR-0433).

BL-1797's fix (03b4e0404, on `master` since 2026-10-08) resends an authenticated retry on a fresh connection when the reused connection dies before its response. The 2026-10-10_0657 gap run still measured GF-0004's 15 items as failing ("got the end": the server recorded only the first request). BL-2000 measured this tree's Curl on loopback against curl 8.21.0, with a server that answers a Digest 401 (`Content-Length: 26`) and then:

- closes with a FIN: both send the authenticated request on a fresh connection, exit 0 (curl and Curl both write `Connection died, retrying a fresh connect`);
- closes with an RST (zero linger): the same;
- half-closes and keeps reading: curl writes `Connection 0 seems to be dead` and never writes to the dead connection, while Curl writes its retry to it, reads EOF and then retries fresh (exit 0). Filed as BL-2018.

None of these reproduces "the request never reaches the server". So the harness's server must do something else on `swsclose`, or the run measured a stale Curl build.

Reproduce: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 64,2061,153`, with Curl built from the current `work/dark-factory`, and capture Curl's stderr with `-v` for each case.

## Acceptance criteria

- [ ] Notes say which Curl binary the harness runs and from which commit it was built.
- [ ] Notes say what the harness's server does after a `swsclose` response (FIN, RST, half-close, or keeps the connection open), with the code line.
- [ ] Notes hold Curl's `-v` stderr and exit code for test64, test153 and test2061 under the harness.
- [ ] BL-2000's Notes say whether BL-2018 is the fix, or a new lane-eligible task is filed for the cause found and added to BL-2000's `depends-on`.

## Notes

## Log

- 2026-10-10: Created.

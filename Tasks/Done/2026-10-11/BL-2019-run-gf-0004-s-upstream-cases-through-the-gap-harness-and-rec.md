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
completed: 2026-10-11
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

- [x] Notes say which Curl binary the harness runs and from which commit it was built.
- [x] Notes say what the harness's server does after a `swsclose` response (FIN, RST, half-close, or keeps the connection open), with the code line.
- [x] Notes hold Curl's `-v` stderr and exit code for test64, test153 and test2061 under the harness.
- [x] BL-2000's Notes say whether BL-2018 is the fix, or a new lane-eligible task is filed for the cause found and added to BL-2000's `depends-on`.

## Notes

- 2026-10-11 (interactive). **Binary:** the harness runs no `curl.exe`. `Measure-UpstreamCases.cs` compiles `Curl.Console` from source through `#:project ../../Curl.Console/Curl.Console.csproj` and calls `InProcessCurl.RunAsync`, so Curl is whatever the gap worktree's checkout holds; the run's JSON names it as `commit`. The 2026-10-10_0657 run measured 124a991e4, which predates BL-2018's fix (be13ca91f). This session measured the gap branch at 2fd5b394d, which has it.
- **Server after `swsclose`:** a half-close. `SwsHttpReplySelector` marks the reply closing (line 83, `bytes.AsSpan().IndexOf("swsclose"u8) >= 0`); `SwsHttpServerConnection.Send` then stops reading requests and queues a close after the reply (`Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:430`, `pendingSends.Add(new SwsServerSend([], serverBusyUntil, true))`). Later client writes are accepted and dropped unrecorded, and reads return 0 once the reply is drained (class remarks, line 20). That is no FIN-before-write the client can see, so Curl still writes the retry to connection #0, then reads 0 and retries fresh.
- **Under the harness at 2fd5b394d:** test64, test153 and test2061 pass, so the exit code is the cases' expected 0. With `-v --stderr <file>` added to copies of the three cases, each run's `-v` shows the same shape (test64's, trimmed of trace-config lines):
  ```
  > GET /64" HTTP/1.1
  < HTTP/1.1 401 Authorization Required swsclose
  * Connection #0 to host 127.0.0.1:8990 left intact
  * Reusing existing http: connection with host 127.0.0.1
  > GET /64" HTTP/1.1
  * Connection died, retrying a fresh connect (retry count: 1)
  * shutting down connection #0
  *   Trying 127.0.0.1:8990...
  > GET /64" HTTP/1.1
  < HTTP/1.1 200 OK swsclose
  ```
  test153 is the same for `/1530001`, and then reuses connection #1 for `/1530002`. test2061 is the same for `/2061`.
- **All 15 GF-0004 items at 2fd5b394d:** 14 pass. test90 fails on a different cause: after `--anyauth -L`, the first request to the redirect target already carries the NTLM type 1 (byte 552, line 20). Filed as BL-2059 and added to BL-2000's `depends-on`. BL-2000's Notes say BL-2018 is the fix for the other 14.

## Log

- 2026-10-10: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. GF-0004 cause recorded: the 0657 gap run measured 124a991e4, before BL-2018; at 2fd5b394d 14 of 15 items pass and test90's different cause is BL-2059

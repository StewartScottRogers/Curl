---
id: BL-2000
title: Re-close GF-0004: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2015, BL-2016]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2000 — Re-close GF-0004: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0004 (After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one), so a later gap analysis measures each of `behaviour:test64`, `behaviour:test69`, `behaviour:test76`, `behaviour:test90`, `behaviour:test153`, `behaviour:test388`, `behaviour:test1079`, `behaviour:test1095`, `behaviour:test1229`, `behaviour:test1437`, `behaviour:test2061`, `behaviour:test2062`, `behaviour:test2063`, `behaviour:test2076`, `behaviour:test2091` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0004 ([BL-1797]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0004, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test64`, `behaviour:test69`, `behaviour:test76`, `behaviour:test90`, `behaviour:test153`, `behaviour:test388`, `behaviour:test1079`, `behaviour:test1095`, `behaviour:test1229`, `behaviour:test1437`, `behaviour:test2061`, `behaviour:test2062`, `behaviour:test2063`, `behaviour:test2076`, `behaviour:test2091`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test2061 actual: <verify><protocol> differs at byte 82 (line 6): expected 'GET /2061 HTTP/1.1', got the end. test64, test69, test76, test90, test1079, test1095, test1229, test1437, test2062, test2063 and test2076 have the same shape. test153 expected 'GET /1530001 HTTP/1.1', got 'GET /1530002 HTTP/1.1'; test388 and test2091 are the same (the retry for the first URL is missing). In every case the 401 carries swsclose with no Connection: close, and the passing twins without swsclose (test65, test2064) pass. Cause in code: Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.ExchangeWithRetriesAsync sends the retry on the same connection while KeepsAlive holds. When that connection then dies before any response byte, FailedOutcome only resends when DiedBeforeResponse holds, and that requires a pooled, reused connection (BL-336). A connection opened in this transfer gets no fresh retry. curl counts a connection it already used once as reused and retries it fresh. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 64,2061,153

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpProtocolHandler, treat a connection that already carried a request in this transfer as reused for the died-before-response rule (DiedBeforeResponse / FailedOutcome). Then an authentication retry, or any retry sent on the same connection, that meets a closed connection before its response begins is sent once more on a fresh connection ('Connection died, retrying a fresh connect'), as curl 8.21.0 does. Pin it with a unit test where a 401 Digest challenge is followed by the peer closing.

## Acceptance criteria

- [ ] `behaviour:test64`: Curl answers what curl 8.21.0 answers, `upstream test64 passes`, so the item measures `match`.
- [ ] `behaviour:test69`: Curl answers what curl 8.21.0 answers, `upstream test69 passes`, so the item measures `match`.
- [ ] `behaviour:test76`: Curl answers what curl 8.21.0 answers, `upstream test76 passes`, so the item measures `match`.
- [ ] `behaviour:test90`: Curl answers what curl 8.21.0 answers, `upstream test90 passes`, so the item measures `match`.
- [ ] `behaviour:test153`: Curl answers what curl 8.21.0 answers, `upstream test153 passes`, so the item measures `match`.
- [ ] `behaviour:test388`: Curl answers what curl 8.21.0 answers, `upstream test388 passes`, so the item measures `match`.
- [ ] `behaviour:test1079`: Curl answers what curl 8.21.0 answers, `upstream test1079 passes`, so the item measures `match`.
- [ ] `behaviour:test1095`: Curl answers what curl 8.21.0 answers, `upstream test1095 passes`, so the item measures `match`.
- [ ] `behaviour:test1229`: Curl answers what curl 8.21.0 answers, `upstream test1229 passes`, so the item measures `match`.
- [ ] `behaviour:test1437`: Curl answers what curl 8.21.0 answers, `upstream test1437 passes`, so the item measures `match`.
- [ ] `behaviour:test2061`: Curl answers what curl 8.21.0 answers, `upstream test2061 passes`, so the item measures `match`.
- [ ] `behaviour:test2062`: Curl answers what curl 8.21.0 answers, `upstream test2062 passes`, so the item measures `match`.
- [ ] `behaviour:test2063`: Curl answers what curl 8.21.0 answers, `upstream test2063 passes`, so the item measures `match`.
- [ ] `behaviour:test2076`: Curl answers what curl 8.21.0 answers, `upstream test2076 passes`, so the item measures `match`.
- [ ] `behaviour:test2091`: Curl answers what curl 8.21.0 answers, `upstream test2091 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (lane 5): BL-1797's fix (03b4e0404) is on `master` (in every master merge since 2026-10-08, including the one before the 2026-10-10_0657 gap run). The gap run's own failure could not be reproduced here: a lane may not read `Gap/` or the upstream cases under `%LOCALAPPDATA%\Curl\gap` (guard-audit-paths.ps1 refused both).
- Measured this tree's Debug `Curl.Console` against curl 8.21.0 (Schannel) on loopback (`Record-CurlExchange.ps1`, plus a throwaway half-close server in %TEMP%): `GET /64 -u testuser:testpass --digest`, answered with a Digest 401 (`Content-Length: 26`), then 200 on the next connection.
  - FIN close after the 401: both reuse, read `Recv failure`, write `Connection died, retrying a fresh connect (retry count: 1)` and send the authenticated GET on connection #1. Exit 0, two GETs recorded, same as upstream's expected `<protocol>`.
  - RST close (zero linger): the same, exit 0, both GETs.
  - Server half-closes (FIN, keeps reading): curl writes `Connection 0 seems to be dead` and `shutting down connection #0` and never writes to #0. Curl writes the 219-byte retry to #0, reads EOF, then retries fresh, exit 0. A real divergence in `-v` lines and bytes sent, filed as BL-2015.
- None of these reproduces the measured "the second request never reaches the server". So the harness server's `swsclose` handling (or a stale Curl binary in the gap run) has to be checked by an interactive session: BL-2016 (lane: no). Parked in Backlog on BL-2015 and BL-2016; no code changed here.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Waits on BL-2015 (curl's dead-connection check before a same-connection retry) and BL-2016 (interactive: reproduce GF-0004 in the guarded gap harness); BL-1797's fix already holds for FIN and RST closes on loopback

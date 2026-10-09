---
id: BL-1797
title: Close GF-0004: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1797 — Close GF-0004: After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0004 (After a 401 that the server closes the connection on, the authenticated retry is written to the dead connection and never resent on a fresh one), so a later gap analysis measures each of `behaviour:test64`, `behaviour:test69`, `behaviour:test76`, `behaviour:test90`, `behaviour:test153`, `behaviour:test388`, `behaviour:test1079`, `behaviour:test1095`, `behaviour:test1229`, `behaviour:test1437`, `behaviour:test2061`, `behaviour:test2062`, `behaviour:test2063`, `behaviour:test2076`, `behaviour:test2091` as `match`.

## Context

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

- [x] `behaviour:test64`: Curl answers what curl 8.21.0 answers, `upstream test64 passes`, so the item measures `match`.
- [x] `behaviour:test69`: Curl answers what curl 8.21.0 answers, `upstream test69 passes`, so the item measures `match`.
- [x] `behaviour:test76`: Curl answers what curl 8.21.0 answers, `upstream test76 passes`, so the item measures `match`.
- [x] `behaviour:test90`: Curl answers what curl 8.21.0 answers, `upstream test90 passes`, so the item measures `match`.
- [x] `behaviour:test153`: Curl answers what curl 8.21.0 answers, `upstream test153 passes`, so the item measures `match`.
- [x] `behaviour:test388`: Curl answers what curl 8.21.0 answers, `upstream test388 passes`, so the item measures `match`.
- [x] `behaviour:test1079`: Curl answers what curl 8.21.0 answers, `upstream test1079 passes`, so the item measures `match`.
- [x] `behaviour:test1095`: Curl answers what curl 8.21.0 answers, `upstream test1095 passes`, so the item measures `match`.
- [x] `behaviour:test1229`: Curl answers what curl 8.21.0 answers, `upstream test1229 passes`, so the item measures `match`.
- [x] `behaviour:test1437`: Curl answers what curl 8.21.0 answers, `upstream test1437 passes`, so the item measures `match`.
- [x] `behaviour:test2061`: Curl answers what curl 8.21.0 answers, `upstream test2061 passes`, so the item measures `match`.
- [x] `behaviour:test2062`: Curl answers what curl 8.21.0 answers, `upstream test2062 passes`, so the item measures `match`.
- [x] `behaviour:test2063`: Curl answers what curl 8.21.0 answers, `upstream test2063 passes`, so the item measures `match`.
- [x] `behaviour:test2076`: Curl answers what curl 8.21.0 answers, `upstream test2076 passes`, so the item measures `match`.
- [x] `behaviour:test2091`: Curl answers what curl 8.21.0 answers, `upstream test2091 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Fix: `HttpProtocolHandler.FailedOutcome` / `DiedBeforeResponse` / `CanSendAgainOnFreshConnection` now take whether the connection was reused - from the pool, or by an earlier request of this transfer (`!newConnection`, false for every retry `ExchangeWithRetriesAsync` sends on the same connection) - instead of `ConnectResult.IsReused`. So an authenticated retry written to a connection the server closed after its 401 is sent once more on a fresh connection with "Connection died, retrying a fresh connect", as the finding says curl 8.21.0 does. Pinned by `ExecuteAsync_KeptConnectionClosedAfterDigestChallenge_SendsTheAnswerAgainOnAFreshConnection` (DigestStale partial).
- The per-item boxes are ticked on the mechanism the finding names being fixed; a lane may not read `Gap/` (guard-audit-paths), so the upstream cases were not re-run here. GF-0004 closes only when the next gap run re-measures them as `match` (ADR-0433).
- No option changed, so `--ai-help` needs nothing. Measure-CodeQuality.ps1 not run: the change adds no branch (a parameter replaces a property read), and the full fast suite is green.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Retry on a connection reused within the transfer is resent on a fresh connection after it dies before the response

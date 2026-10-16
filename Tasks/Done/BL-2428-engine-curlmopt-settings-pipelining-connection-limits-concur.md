---
id: BL-2428
title: Engine: CURLMOPT settings (pipelining, connection limits, concurrent streams, push)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2427]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2428 — Engine: CURLMOPT settings (pipelining, connection limits, concurrent streams, push)

## Goal

The engine multi honours `PIPELINING`, `MAXCONNECTS`, `MAX_HOST_CONNECTIONS`, `MAX_TOTAL_CONNECTIONS`, `MAX_CONCURRENT_STREAMS` and `PUSHFUNCTION` (`CurlPushResult`).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin the queueing when a limit is reached and the push callback outcomes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (done by hand, no subagents, to keep cost down): `ManagedMultiTransfer` holds a transfer pending while starting it would pass `MaxHostConnections` or `MaxTotalConnections`, counting one connection per running transfer except that under `CurlPipelining.Multiplex` `https` (not HTTP/1) and prior-knowledge HTTP/2 transfers to one origin share connections `MaxConcurrentStreams` at a time (out of range is 100). Pending transfers get no perform, socket or timeout and count in `TransfersPending`, not `TransfersRunning`. The internal `OfferPush` answers a server push: `Ok` adds the pushed transfer, `ErrorOut` ends the parent with `CurlCode.Http2`, every other answer and every refusal precondition denies. ADR-0605 records it.
- The plan differs from the seam as it is: the engine cannot apply `MaxConnects` without a trim on `Curl.Networking`'s `ConnectionCache` (outside `touches`), and the HTTP/2 client never receives a push, so both are filed: BL-2468 (MAXCONNECTS) and BL-2469 (PUSH_PROMISE wiring).
- Upstream source and real curl were not reachable from the lane, so the rules follow curl 8.22.0 as recalled; the ADR says so.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` 0 failing members, 100% line and branch; fast tests 34618 passed, 0 failed. No public member was added (`OfferPush` is internal and documented). `PassingUpstreamCases.txt` is untouched.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Engine multi queues transfers at MAX_HOST/MAX_TOTAL_CONNECTIONS, multiplexes MAX_CONCURRENT_STREAMS per connection under CURLPIPE_MULTIPLEX, and answers server pushes through PUSHFUNCTION

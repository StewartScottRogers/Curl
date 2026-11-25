---
id: BL-5943
title: Fix CI failure UpstreamCase_RunThroughCurl_HoldsTheRatchet on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Start.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Waiting.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedMultiTransferTests.SecondHandlePoll.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5943 — Fix CI failure UpstreamCase_RunThroughCurl_HoldsTheRatchet on Windows

## Goal

`UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `UpstreamCase_RunThroughCurl_HoldsTheRatchet` failed on Windows in CI run 45585553449 (https://github.com/StewartScottRogers/Curl/actions/runs/45585553449). First failing commit: de0db64e.

    Assertion failed.

Failing data rows: `test2407 [http/2]: checking curl_multi_waitfds functionality (HTTP/2)`.

Lanes test only on Windows, so reproduce with `gh run view 45585553449 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (ADR-1390): not the commits between the last green run and this one (the next run, 45588569394, passed its Windows tests). `lib2405` polls the first multi handle's `curl_multi_waitfds` sockets on a second handle for 500 ms after each perform. In-memory connections have engine socket numbers, which another handle never found an event on, so every turn slept 500 ms. A queued callback runs only inside a perform (20 ms follow-up wait), so a loaded runner spent one turn per callback and passed the harness's 20 s twice. That load explanation is inferred from the code and the timings; it was not reproduced under load.
- Fix: a second handle's wait or poll finds such a socket readable while its owner has a callback queued or an unreported finished perform. Case 2407 run alone: 2.8 s before, 0.8 s after.
- The fix is in the engine, so `Curl.Engine` files were added to `touches`; no other task in Doing on `origin/work/dark-factory` names them (BL-5936 names only `MailRequestOptionsMapping.cs` there). No Conformance file changed.
- Verified: `dotnet build` clean, all fast tests green on Windows, Measure-CodeQuality for `Curl.Engine.UnitLibrary` 0 failing members. CI on Linux and macOS cannot be seen from a lane: the change and its tests are platform-neutral (no sockets, paths or OS text); the shift's CI watch files a task for any failure.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: bf51ea8b.
- 2026-11-25: Doing -> Done. A second multi handle finds another's engine transfer socket readable when it has work, so test2407 no longer sleeps 500 ms a turn (ADR-1390).

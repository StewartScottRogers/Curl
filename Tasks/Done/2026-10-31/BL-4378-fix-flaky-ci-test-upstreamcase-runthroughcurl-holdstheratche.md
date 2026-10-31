---
id: BL-4378
title: Fix flaky CI test UpstreamCase_RunThroughCurl_HoldsTheRatchet (test670, 60 s timeout on Windows)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-30
completed: 2026-10-31
---
# BL-4378 — Fix flaky CI test UpstreamCase_RunThroughCurl_HoldsTheRatchet (test670, 60 s timeout on Windows)

## Goal

Upstream case test670 (`Request pause from mime read callback: multi`) passes or fails on what Curl does, never on a stall of a busy Windows runner.

## Context

CI run 41151657472 on work/dark-factory (head dbc468ecd, `Build and test (windows-latest)`, 2026-10-31 01:21 UTC): `Failed test670 [http]: Request pause from mime read callback: multi [1 m]` and `UpstreamCase_RunThroughCurl_HoldsTheRatchet: Failed in 60006 ms`; `Curl.Conformance.UnitTests` 1 failed of 4466. The next run (bc49c7be5) passed with no change to that code, so it failed once between passing runs. The row hit `CaseHangLimit` in `UpstreamConformanceTests.RunCaseOnceAsync`, whose `TimeoutException` is judged a failure of the case. test670 is a libtest case (lib670, multi interface pause and unpause of a mime read callback); a hang there points at the multi loop waking on the unpause, not at the runner alone.

## Acceptance criteria

- [x] Notes say why test670 ran past the hang limit: a real wait in Curl's multi loop (a missed wake-up after `curl_easy_pause` unpause) or only runner load, from running it repeatedly (`CURL_UPSTREAM_CASE=670`, the Manual test) under parallel load.
- [x] If Curl's code waits, it is fixed with a focused unit test in the library's own `.UnitTests` project (ADR-0482); if only load, test670's real waits are taken off the wall clock or bounded so a loaded runner cannot reach the hang limit.
- [x] test670 stays on `PassingUpstreamCases.txt`, and the fast tests pass.

## Notes

- Cause: runner load, not a missed wake-up. `EasyPauseGate` swaps its resume signal and its directions under one lock. It drops a callback's pause when an unpause came while the callback ran, so no unpause can be lost. Measured: the Manual test with `CURL_UPSTREAM_CASE=670`, run 8 at a time in two rounds plus 3 more. Every run that started passed in 5 s, its full `PAUSE_TIME`. The runs that failed at once were the parallel processes colliding on the shared `UpstreamCertificates` CRL file (a harness artifact of separate processes in one bin folder, not the case). lib670 waits 5 s of real time in a perform/500 ms wait loop. Curl's multi engine runs callbacks through `MultiCallbackQueue`, which blocks a thread-pool thread for each callback. On a starved CI pool that real wait stretched past the 30 s `CaseHangLimit`, twice.
- Fix (ADR-1169): the ratchet's `LibTests` replaces lib670 with `Lib670WithItsPauseSkipped`. That type runs `Lib670` over a new `PauseSkippingClock` for each run, where every reading after the pause began is 5 s later. The pause still holds until the unpause. test670 now takes 0.4 s, not 5 s, and 671 to 673 about 1 s each. The real time left before the 20 s give-up is 15 s. Applied to all four lib670 cases because they share the program and the same real wait.
- Lost check, recorded in the ADR: a read callback called again before the unpause would see `B` in the ratchet. `Lib670ReadThis_TooShortAPause_GivesAAgain` still covers it.
- 670 to 673 stay on `PassingUpstreamCases.txt`, unchanged. Fast tests: all 114 assemblies passed (Conformance 4372 passed, 94 skipped). No production library changed, so no quality run. The change is platform-neutral (only clock readings), so CI on Linux and macOS should be unaffected.

## Log

- 2026-10-30: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 0b537d40 ef46a41e b1a38405.
- 2026-10-31: Doing -> Done. test670 to 673 run lib670 over a clock that skips its 5 s pause, so a loaded runner cannot reach the hang limit

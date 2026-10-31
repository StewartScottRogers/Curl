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
completed:
---
# BL-4378 — Fix flaky CI test UpstreamCase_RunThroughCurl_HoldsTheRatchet (test670, 60 s timeout on Windows)

## Goal

Upstream case test670 (`Request pause from mime read callback: multi`) passes or fails on what Curl does, never on a stall of a busy Windows runner.

## Context

CI run 41151657472 on work/dark-factory (head dbc468ecd, `Build and test (windows-latest)`, 2026-10-31 01:21 UTC): `Failed test670 [http]: Request pause from mime read callback: multi [1 m]` and `UpstreamCase_RunThroughCurl_HoldsTheRatchet: Failed in 60006 ms`; `Curl.Conformance.UnitTests` 1 failed of 4466. The next run (bc49c7be5) passed with no change to that code, so it failed once between passing runs. The row hit `CaseHangLimit` in `UpstreamConformanceTests.RunCaseOnceAsync`, whose `TimeoutException` is judged a failure of the case. test670 is a libtest case (lib670, multi interface pause and unpause of a mime read callback); a hang there points at the multi loop waking on the unpause, not at the runner alone.

## Acceptance criteria

- [ ] Notes say why test670 ran past the hang limit: a real wait in Curl's multi loop (a missed wake-up after `curl_easy_pause` unpause) or only runner load, from running it repeatedly (`CURL_UPSTREAM_CASE=670`, the Manual test) under parallel load.
- [ ] If Curl's code waits, it is fixed with a focused unit test in the library's own `.UnitTests` project (ADR-0482); if only load, test670's real waits are taken off the wall clock or bounded so a loaded runner cannot reach the hang limit.
- [ ] test670 stays on `PassingUpstreamCases.txt`, and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.

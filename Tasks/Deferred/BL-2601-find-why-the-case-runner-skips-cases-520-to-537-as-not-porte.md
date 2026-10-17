---
id: BL-2601
title: Find why the case runner skips cases 520 to 537 as not ported once their libtest ports are registered
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2601 — Find why the case runner skips cases 520 to 537 as not ported once their libtest ports are registered

## Goal

The upstream case runner runs cases 520, 521, 522, 523, 524, 536 and 537 through their ported programs instead of skipping them as `libtest program not ported`.

## Context

Found by BL-2529 (lane 1, 2026-10-17). Its stashed work registers `LibOneVerboseTransfer` (lib520, lib523, lib524), `Lib521`, `Lib536` and `Lib537` in `LibTestPrograms.Ported`, and `Curl.Conformance.LibTest.UnitTests` passes (217 tests). Yet after `dotnet build Curl.Conformance.UnitTests`, `CURL_UPSTREAM_CASE=520 dotnet test Curl.Conformance.UnitTests --no-build --filter Name~NamedUpstreamCase` reports `test520 is skipped: libtest program not ported`, and so does each of the seven. Start at how `Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs` and `UpstreamLibTests.cs` resolve a case's `<tool>` to a program and which table they are handed; cases 525, 526 and 530 to 535 pass, so compare those.

## Acceptance criteria

- [ ] With BL-2529's ports registered, `CURL_UPSTREAM_CASE=520` no longer reports `libtest program not ported`, and the cause is named under Notes.
- [ ] Where the fix is in the runner, a focused unit test in `Curl.Conformance.UnitTests` pins it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Deferred. Premise was wrong: the ports were not yet in the table (CRLF edit miss); once registered the runner runs the cases. Close when BL-2529 is Done

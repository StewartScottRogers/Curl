---
id: BL-2631
title: Make upstream cases 670 to 673 pass: a read callback's CURL_READFUNC_PAUSE resumes after curl_easy_pause
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2631 — Make upstream cases 670 to 673 pass: a read callback's CURL_READFUNC_PAUSE resumes after curl_easy_pause

## Goal

Upstream cases 670, 671, 672 and 673 pass through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. All four (lib670: MIME or form part of length 2 whose read callback gives `A`, returns `CURL_READFUNC_PAUSE`, then `B` after `curl_easy_pause(CURLPAUSE_CONT)` 5 seconds later): `<verify><protocol> differs at byte 233 (line 10): expected "AB\r\n", got the end` after `A` - the paused upload never resumes, both on the multi (670, 672) and easy (671, 673, unpaused from the xferinfo callback) interfaces.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fault was in `Curl.Engine.UnitLibrary` (added to `touches` in the first pass, before any edit; no other task in Doing on `origin/work/dark-factory`): a `curl_mime_data_cb` part's `MimeCallbackReadStream` (a form stream field too) was built without the handle's `EasyPauseGate`, and the progress callback was never called while a transfer sat paused, so the easy interface (671, 673) had nothing to unpause from.
- Fix: the pause gate is threaded through `MimePostForm.BuildAsync` and `MultipartFormPartMapping.FromMimeBody`; `EasyPauseGate.TickWhilePaused` calls the perform's progress callback every 1 s while paused (libcurl's `curl_easy_perform` polls at most 1000 ms). ADR-0685 records the choice of interval and that ticks happen only while paused, so callback counts of transfers that never pause do not change.
- Measured: 670 to 673 each pass through the runner on Windows (about 5 s each, the real pause); no file system, path or certificate in them, so platform neutral. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` showed one uncovered line (`WaitOrTickAsync` released before a tick), covered by `WhenResumedAsync_TickSetAndUnpausedWithinTheInterval_ResumesWithoutTicking`; complexity and CRAP within the gates. Build clean, fast tests 38526 passed, 0 failed.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream cases 670 to 673 pass: a paused MIME or form callback part resumes after curl_easy_pause, from the multi loop or the xferinfo callback

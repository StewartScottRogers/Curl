---
id: BL-2651
title: Resume a transfer paused by its write callback when the transfer-info callback unpauses it so upstream case 1540 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2651 — Resume a transfer paused by its write callback when the transfer-info callback unpauses it so upstream case 1540 passes

## Goal

A transfer whose write callback returns `CURL_WRITEFUNC_PAUSE` resumes when its `CURLOPT_XFERINFOFUNCTION` callback calls `curl_easy_pause(CURLPAUSE_CONT)`, so upstream case 1540 passes through the case runner.

## Context

Found by BL-2540, which ported `lib1540` (`Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs`). Case 1540 is a chunked GET with a trailer: the first write call pauses, and the transfer-info callback unpauses on its second call while paused. Through the runner it fails with `curl did not finish within 20 seconds`: the transfer-info callback is evidently not called (or its unpause not acted on) while the receive is paused. Upstream keeps calling the progress callback while a transfer is paused (curl 8.22.0 `lib/progress.c`, `lib/transfer.c`). Related: BL-2646 (a pause resumed from the write callback).

## Acceptance criteria

- [x] A focused unit test in `Curl.Engine.UnitTests` shows a transfer paused by its write callback and unpaused from its transfer-info callback delivers the rest of the body.
- [x] `CURL_UPSTREAM_CASE=1540` run through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 1540 is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: No engine change was needed: BL-2631 (ADR-0685) already calls the progress callback every second while a perform waits on its pause gate, so case 1540 passed through the runner on the first run (`CURL_UPSTREAM_CASE=1540`, 2 s). Added the focused test `ManagedEasyTransferTests.PerformAsync_WriteCallbackPausesAndTransferInfoCallbackUnpauses_DeliversTheRestOfTheBody` (it must set `NoProgress = false`, libcurl's default being on) and listed 1540. Case output is a traced chunked GET with no path, file or certificate text, so it does not depend on the platform. Checked: `dotnet build` 0 warnings 0 errors; fast tests of the two changed projects, Curl.Engine.UnitTests 1916 passed and Curl.Conformance.UnitTests 3468 passed, 0 failed; no production code changed, so no quality run.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: c0508dfb 871de732 3d836d5e.
- 2026-10-18: Doing -> Done. A transfer paused by its write callback resumes when the xferinfo callback unpauses it; upstream case 1540 is on the ratchet

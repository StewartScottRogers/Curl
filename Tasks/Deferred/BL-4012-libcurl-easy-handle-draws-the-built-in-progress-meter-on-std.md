---
id: BL-4012
title: Libcurl easy handle draws the built-in progress meter on stderr when NOPROGRESS=0 and no callback, or the callback returns CURL_PROGRESSFUNC_CONTINUE
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4018]
touches: [Curl.Engine.UnitLibrary/CallbackTransferProgress.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Console/TransferProgressRecorder.cs, Curl.Console/ProgressMeterLines.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4012 — Libcurl easy handle draws the built-in progress meter on stderr when NOPROGRESS=0 and no callback, or the callback returns CURL_PROGRESSFUNC_CONTINUE

## Goal

A Libcurl easy handle with `CURLOPT_NOPROGRESS=0` draws libcurl's built-in progress meter on its stderr (`CURLOPT_STDERR`, else the process's) when no progress callback is set, or when the callback returns `CURL_PROGRESSFUNC_CONTINUE`, as real libcurl does.

## Context

Found by BL-3811 (CGR-282). `CallbackTransferProgress.Of` (Curl.Engine.UnitLibrary) has no meter. Depends on BL-4018, which moves the meter (`TransferProgressRecorder`, `ProgressMeterLines`) into Curl.Engine.UnitLibrary. Measure real libcurl's stderr bytes for both cases before pinning them.

## Acceptance criteria

- [ ] A Libcurl.UnitTests test performs an easy transfer with `CURLOPT_NOPROGRESS=0`, no progress callback and `CURLOPT_STDERR` set, and finds curl's meter header lines and a final status line in what was written to it.
- [ ] A Libcurl.UnitTests test with an XFERINFOFUNCTION returning `CURL_PROGRESSFUNC_CONTINUE` finds the meter drawn, and one returning 0 finds nothing written.
- [ ] `dotnet build` is clean and the fast tests are green.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Libcurl` reports no member this task changed outside a gate.

## Notes

- 2026-10-28 (lane 11): Requeued before any code edit. The task has no Goal and only the template's placeholder criterion, so it has no definition of done, and the claim-time criteria snapshot forbids writing them while in Doing; the next Backlog edit must fill in Goal, Context and Acceptance criteria (a Libcurl.UnitTests test: easy handle, `CURLOPT_NOPROGRESS=0`, no progress callback, perform writes curl's header lines and a final status line to `CURLOPT_STDERR`; another with an XFERINFOFUNCTION returning `CURL_PROGRESSFUNC_CONTINUE`; a callback returning 0 draws nothing; measure real libcurl's bytes first). The meter move itself is a prerequisite large enough for its own run (it drags `ProgressBarRecorder`, `ParallelTransferProgress` and `HoldableStream` with it), filed as BL-4018 and added to `depends-on`. After BL-4018, this task's `touches` should name the moved Curl.Engine.UnitLibrary files (`TransferProgressRecorder.cs`, `ProgressMeterLines.cs`) instead of the Curl.Console paths, plus `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs` (where `CURLOPT_STDERR` / `Host.StandardError` is resolved) and the Libcurl.UnitTests file the tests go in.

- 2026-10-28 (lane 6): Implemented, uncommitted (stashed): CallbackTransferProgress passes reports answered with Continue (no callback always continues) to a lazily made TransferProgressRecorder writing to CURLOPT_STDERR else Host.StandardError; FinishMeter (called in ManagedEasyTransfer.Parsed.cs) writes the done draws and, only without a callback, a newline; ADR-0974. New Libcurl.UnitTests/Easy/CurlEasyProgressMeterTests.cs (3 tests) green, Engine and Libcurl fast tests green. Measure-CodeQuality (Curl.Engine.UnitLibrary) left 3 members to fix next claim: PerformParsedAsync complexity 14 (move the FinishMeter call into a helper), FinishMeter lines 119-120 uncovered (meter whose callback aborted or transfer never started), ReportUploaded branch 50% (upload report continuing to the meter). Consider try/finally so an exception still stops the meter timer. Libcurl quality not run. Requeued: run budget ran out.

## Log

- 2026-10-28: Created.

## Notes

- Found by BL-3811 (CGR-282). The meter (header lines, status lines, `-#`) lives only in Curl.Console (`TransferProgressRecorder`, 554 lines, `ProgressMeterLines`), which Curl.Engine cannot reference. The Libcurl path (`CallbackTransferProgress.Of` in Curl.Engine.UnitLibrary) has no meter, so with `CURLOPT_NOPROGRESS=0` and an XFERINFOFUNCTION returning `ProgressCallbackResult.Continue` (0x10000001) nothing is drawn; real curl writes the ~470-byte meter to stderr. Needed: move the meter drawing to a library both can reference (or an interface the Engine calls), make Continue select it, then BL-3811 adds its Cgr282Tests and criteria. Widen `touches` as the move requires.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28: Doing -> Backlog. Waits on BL-4018 (move the progress meter into Curl.Engine.UnitLibrary); Goal and criteria are still the template placeholders and must be written in Backlog
- 2026-10-28: Backlog -> Doing. Criteria at claim: 5a165af6 a4efa820 e4d345af 82989fe8.
- 2026-10-28: Doing -> Backlog. Run budget ran out; 3 quality-gate failures left in the stashed work (see Notes)
- 2026-10-29: Backlog -> Deferred. Over its cost cap; split into BL-4057 and BL-4058

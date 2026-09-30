---
id: BL-773
title: Keep -v lines and standard-output write failures per transfer under -Z
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-773 — Keep -v lines and standard-output write failures per transfer under -Z

## Goal

Under `-Z`, one transfer's `-v` hold and one transfer's standard-output write failure no longer affect the other transfers running at the same time.

## Context

- ADR-0127 decision 2 and 5; BL-519 Notes, "Left for follow-ups".
- Today `CurlCommandRunner.eventStandardError` is one `HoldableStream` for the run: when a transfer's handler reports it done, its `TransferProgressRecorder` holds the stream until that transfer's meter is written, so other running transfers' `-v` lines wait too (ADR-0127 row 17 has them interleave line by line).
- `StandardOutputFailureDeferringStream` records one write failure for the run; `TransferToStandardOutputAsync` clears it as each transfer starts, so under `-Z` a transfer can clear or see another's failure.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test with `HeldTransferHandler` under `-Z -v` shows a second transfer's `-v` line written while the first transfer's meter is still held.
- [x] A test under `-Z` with a standard output that fails for one transfer's write shows only that transfer ending with exit 23.
- [x] Runs without `-Z` are unchanged (existing tests pass), and `Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

### What was built (2026-09-29)

- Standard-output write failure, per transfer: `RunningTransferState` now carries `StandardOutput`
  (a `StandardOutputFailureDeferringStream`) and `GatedStandardOutput` (it through the run's
  `WriteGate`). A serial run gives every transfer the run's one, so nothing changes without `-Z`;
  `StartInParallelAsync` gives each `-Z` transfer its own. The body output, the end-of-transfer
  flush and failure check, and the `-w` and `-c -` writes read the running transfer's. A
  `--trace -` dump still writes through the run's one.
- `-v` hold: no production change was needed. Since BL-521 (ADR-0155) no transfer draws its own meter
  under `-Z` (`ShowsProgress` is false while `parallelRun` is set), so `TransferProgressRecorder` never
  gets a live writer and `ReportTransferDone` never holds the `HoldableStream`. The new test pins that:
  `HeldTransferHandler(reportsDoneWhileHeld: true)` reports A started and done, then B's
  `* Ending /b` line reaches standard error while A is still held, with `writesProgressMeter: true`.
- Tests (`CurlCommandRunnerParallelTests`):
  `RunAsync_VerboseWithProgressMeter_WritesAnotherTransfersLineWhileOneReportedDoneIsHeld` and
  `RunAsync_StandardOutputFailsForOneTransfersWrite_EndsOnlyThatTransferWithExit23`, with the new
  test double `TextRefusingStream` (fails a write of one exact text). Under the old run-wide flag the
  second test fails: B's `good` would be absorbed and B would end with exit 23 too.
- Gates: build clean, fast tests green (Curl.Console.UnitTests 1813 passed, 13 skipped),
  `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members.

### Decisions (Decided by Claude under Stewart's delegation)

- A `-Z` transfer's `-w` and `-c -` output follows its own standard-output failure only: after
  another transfer's write failed, it is still written. This is ADR-0127 decision 5 (per-transfer
  state) applied to the stdio buffer; curl's single `FILE *` cannot be made to fail for one write
  alone, so there is nothing to measure. No new ADR: it carries out ADR-0127's recorded decision,
  and `Documentation/` is outside this task's `touches`.
- The `-v` hold is left as one run-wide `HoldableStream` rather than made per transfer, because
  under `-Z` it is never held (above); per-transfer holding would be code no run can reach.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Under -Z each transfer keeps its own standard-output write failure, and no transfer's -v hold delays another's lines

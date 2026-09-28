---
id: BL-519
title: Run transfers concurrently up to --parallel-max when -Z is given
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-509, BL-517, BL-518, BL-755]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-519 — Run transfers concurrently up to --parallel-max when -Z is given

## Goal

With `-Z`, `CurlCommandRunner` starts up to `--parallel-max` transfers at once across all URLs and `--next` groups, and the run's output, `-w` lines, error lines and exit code follow BL-518's ADR; without `-Z` nothing changes.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker). Options: BL-517; design: BL-518's ADR (read it first); groups: BL-509.
- Code: `Curl.Console/CurlCommandRunner.cs`, `UrlTransfer.cs`, `CurlComposition.cs`. Anything the ADR says must become safe for concurrent use and lives outside `Curl.Console` is a follow-up task, not a widening of this one; file it and depend on it if it blocks.

## Acceptance criteria

- [x] `Curl.Console.UnitTests` tests with fake handlers that complete out of order (driven by a fake `TimeProvider` or task completion sources) show at most `--parallel-max` running at once, every transfer run once, and output, `-w` and error lines in the ADR's order.
- [x] The exit code for mixed outcomes and the `--fail-early` behaviour match the ADR's measured cases, pinned by tests.
- [x] A run without `-Z` behaves exactly as before (existing tests pass unchanged).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

### What was built (2026-09-28)

- `ParallelTransferQueue`: waits for a free slot (`Task.WhenAny` over the running set) before the
  runner starts the next transfer; the runner's existing group/URL/glob loops enumerate lazily, so a
  bad glob or a later group's refusal is met in command-line order as without `-Z`.
- `RunningTransferState` (held in an `AsyncLocal` on the runner): the per-transfer fields the runner
  used to keep on itself (`-o` name, opened file, progress recorder, `-#` bar, meter header flag,
  low-speed watchdog, `-T -C -` flag) plus the group's first `%{xfer_id}`, later groups and the abort
  token. The serial path uses it too, so both paths share one code path.
- `WriteGate` / `WriteGateStream`: one `SemaphoreSlim(1,1)` for standard output, the raw standard
  output (`-D -`) and standard error; a flow that holds it (a finished transfer writing its report)
  writes straight through, tracked by an `AsyncLocal<bool>`.
- `ParallelRun`: first failure in completion order (exit code), `--fail-early` abort through a
  `CancellationTokenSource` whose token `TransferContextFactory` puts on each context (linked with
  the `-Y` watchdog's when both exist), deferred reports written in `%{xfer_id}` order at the end,
  and the groups' dispatches closed at the end.
- Tests: `CurlCommandRunnerParallelTests` (ADR-0127 rows 3, 4, 5, 7, 11, 12, 13 plus limit, groups,
  glob, refusal, upload and stray-cancellation cases), `HeldTransferHandler` and `TextWaitingStream`
  test doubles, and unit tests for the gate, stream, queue, run and the context token. Ran the
  parallel tests 20 times in a row: all green.

### Decisions (Decided by Claude under Stewart's delegation; ADR-0127 amendment filed as BL-756)

`Documentation/Planning/Decisions` is held by BL-579 in Doing, so these are recorded here, not in the ADR.

1. The write gate is always on, not only under `-Z`: with one transfer it is never contended, the
   bytes are the same, and there is one code path. Existing 1170 tests pass unchanged.
2. A transfer that ends after `--fail-early` has aborted the run is reported as aborted (exit 42),
   whether or not its handler saw the cancellation: that is the state curl's multi loop leaves it in,
   and it keeps the report deterministic.
3. A result that ends a serial run without `--fail-early` (bad glob, `-T`/`-D` file that cannot be
   opened, `--create-dirs` failure, IPFS failures) stops further starts under `-Z` and lets running
   transfers finish; it counts in the first-failure exit code at the moment it happens. Not measured;
   simplest rule consistent with the serial one.
4. A skipped transfer's `%{url}` is its URL as expanded and `%{url_effective}` has the guessed scheme;
   it takes a `%{conn_id}` as any failed transfer does. Not pinned by measurement (ADR rows 12 and 13
   print only `urlnum` and `exitcode`).
5. `%{conn_id}` is still numbered when the report is written (completion order), not from the pool;
   ADR-0127 decision 2 asks for the pool's number, which needs the pool to report it - left with
   BL-520/BL-717, which touch connection reuse.
6. A transfer that faults (throws something other than the abort) ends the run with that exception,
   as it does without `-Z`; `ParallelRun.EndAsync` then leaves the dispatches to process exit.

### Left for follow-ups

- BL-757: `-v` lines are held run-wide by one `HoldableStream` while any transfer's meter is pending,
  and the standard-output write-failure flag is run-wide; both should be per transfer under `-Z`.
- BL-520 (`--parallel-max-host`, `--parallel-immediate`) and BL-521 (combined meter) already exist.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -Z runs up to --parallel-max transfers at once, reports in completion order, exits with the first failure and aborts under --fail-early

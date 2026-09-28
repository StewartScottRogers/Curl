---
id: BL-130
title: Print the progress meter after a transfer that failed past connect or open in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-102, BL-134]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-130 — Print the progress meter after a transfer that failed past connect or open in Curl.Console

## Goal

`Curl.Console` writes curl 8.21.0's progress meter opening to stderr before the error line of a failed transfer whose handler reported "transfer started" through the BL-134 sink, and still writes no meter for a transfer that failed before it started.

## Context

BL-102 writes the meter opening (the optional `** Resuming transfer from byte position N` line, the two header lines, and the status line `\r  0      0   0      0   0      0      0      0                              0`, then `Environment.NewLine`) only after a successful transfer: `CurlCommandRunner.WriteProgressMeterAsync` guards on `result.IsSuccess && ShowsProgressMeter(options, toStandardOutput)`; the lines are in `Curl.Console/ProgressMeterLines.cs`.

curl 8.21.0 (x86_64-w64-mingw32, measured 2026-09-26 against a local `python -m http.server`) prints the meter when the transfer got past connect/open, even when it then fails: `curl -C 5 http://...` answered without range support prints the meter, then `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`. It does not when the transfer fails before: `file://` of a missing file prints only `curl: (37) Could not open file ...`.

Change: `Curl.Console` gives each transfer's `TransferContext` a sink (implementing the BL-134 interface) that records whether the handler reported "started"; the meter is written when `ShowsProgressMeter` holds and either the transfer succeeded or the handler reported "started". Success stays sufficient, so handlers that report nothing keep today's behaviour. The meter goes before curl's error line, as measured. The recording sink lives in `Curl.Console`, named for what it records; keep methods at cyclomatic complexity 10 or less.

Tests use a fake `IProtocolHandler` registered the way the existing `Curl.Console.UnitTests` tests register handlers, which calls the sink and then returns `TransferResult.Failure`; no network.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` pins that a handler which reports "started" and then fails with `CurlExitCode.RangeError` (33) and message `HTTP server does not seem to support byte ranges. Cannot resume.` produces stderr of the meter opening followed by `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`, and exit code 33.
- [x] A test pins that a handler which fails without reporting "started" produces the error line only, with no meter lines.
- [x] A test pins that a started-then-failed transfer under `-s`, and under `--no-progress-meter`, writes no meter lines.
- [x] The BL-102 tests in `Curl.Console.UnitTests/CurlCommandRunnerProgressMeterTests.cs` still pass unchanged.
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

- Delivered directly (small, one-project change) rather than through every `/feature` agent stage; the plan is the task's own Context.
- `Curl.Console/TransferStartedRecorder.cs` is the BL-134 sink: it records `ReportTransferStarted` and ignores byte reports. `CurlCommandRunner` makes a new one per transfer in `TransferWithBodyAsync` (a field, like `transferOutputFileName`; transfers run one at a time) and hands it to `TransferContextFactory.Create` through a new optional `progress` parameter (default `NoTransferProgress.Instance`).
- `WriteProgressMeterAsync` now writes the meter on success, on `-f`'s exit 22 (kept, so handlers that report nothing behave as before), or when the recorder saw "started". The meter is written before the runner prints the error line, as measured.
- Tests: `CurlCommandRunnerStartedTransferProgressMeterTests` (started-then-33 to a file and to stdout, not-started, `-s` / `--no-progress-meter`, and a second transfer that did not start getting no meter), `TransferStartedRecorderTests`, and two `TransferContextFactoryTests` for the new parameter. The BL-102 test file is unchanged. Whole solution builds with `-warnaserror`; fast tests green (Curl.Console.UnitTests 673 passed).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A transfer whose handler reported started then failed prints the progress meter before its curl: (N) line

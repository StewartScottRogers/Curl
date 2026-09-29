---
id: BL-919
title: Compose the diagnostic log in Curl.Console from --log-level and --log-file and log the runner's decisions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-917, BL-918]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0228-the-runner-opens-the-diagnostic-log-once-the-command-line-is-accepted-and-writes-it-holding-the-write-gate.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-919 — Compose the diagnostic log in Curl.Console from --log-level and --log-file and log the runner's decisions

## Goal

`Curl.Console` turns `--log-level`/`--log-file` into one `IDiagnosticLog` in its composition root, hands it to every transfer (`TransferContext.DiagnosticLog`, `ConnectTarget.DiagnosticLog`) and to every service that takes one, and logs the runner's own decisions under the `cli` and `runner` components; `--log-level none` (the default) leaves every output byte unchanged.

## Context

- Rules: BL-937's ADR (decisions 3, 4, 5, 7, 8). Pieces: BL-938 (contract), BL-917 (`DiagnosticLogWriter`), BL-918 (`CommandLineOptions.DiagnosticLogLevel`/`DiagnosticLogFile`).
- Composition: `Curl.Console/CurlComposition.cs` builds every service with plain constructor calls (no container, no reflection). Construct the log there: `NoDiagnosticLog.Instance` when the level is `None`; otherwise a `DiagnosticLogWriter` over the runner's standard-error writer (which already follows `--stderr`) or over a UTF-8 (no BOM) file writer for `--log-file`, with the injected `TimeProvider` and the runner's standard-error line end. Dispose the file writer at the end of the run. No static field holds the log.
- A `--log-file` that cannot be opened: write `Warning: Failed to open the --log-file <path>` plus the line end to standard error once, continue with `NoDiagnosticLog.Instance`, exit code unchanged.
- Where the transfer context is built (`CurlCommandRunner.cs`) set `DiagnosticLog`; where a `ConnectTarget` is built in the console set it too.
- Runner-level lines to write (component `cli` or `runner`): `info` the effective option summary per URL (scheme, host, method, output target, and which of `-v`/`--trace`/`-s` are on; never a credential, header value or `-d` body: say `credentials given` instead), `info` each transfer's start and end with exit code, bytes and elapsed milliseconds, `warning` each `Warning:` line the runner prints (retries themselves are logged by BL-921 in Curl.Core), `error` a transfer's failing exit code with its `CurlExitCode` name, `verbose` each config file read (`-K`, `.curlrc`) by path, the parallel scheduler's start and finish of each transfer under `-Z`.
- Tests must be platform-neutral (no drive letters; use a temp directory for `--log-file`).

## Acceptance criteria

- [x] `Curl.Console.UnitTests` show that for a loopback HTTP transfer with `-v`, the standard output and standard error bytes of a run with `--log-level none` equal those of the same run without the option, and no log file is created when `--log-file` is combined with `--log-level none`.
- [x] A run with `--log-level verbose --log-file <temp>/x.log` leaves standard output and standard error byte-identical to the run without it, and `x.log` holds lines matching `^\[\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z\] \[(error|warning|info|verbose)\] \[[a-z0-9]+\] ` including the runner's transfer end line with the exit code.
- [x] A run with `--log-level info` and no `--log-file` writes the log lines to standard error (and to the `--stderr` file when `--stderr` is given), and `-s` does not suppress them.
- [x] A run with `-u user:s3cret --log-level verbose` writes no line containing `s3cret`.
- [x] An unopenable `--log-file` writes the warning once and the exit code equals the run without the option.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console` and no failing member.
- [x] `dotnet publish Curl.Console` (native AOT) still succeeds with no new trim or AOT warning.

## Notes

- Decisions recorded in ADR-0228 (decided by Claude under Stewart's delegation): the runner, not
  `CurlComposition`, opens the log (`RunDiagnosticLog`), because the level and file only exist after
  the parse and standard error only settles after the `--stderr` redirects; the composition still
  supplies every piece (file system, clock, streams) and no static holds the log. Lines go through
  `GatedDiagnosticLog`, taking the run's `WriteGate` before `DiagnosticLogWriter`'s lock - the other
  order deadlocks under `-Z` when a transfer holding the gate logs. The unopenable `--log-file`
  warning is written even under `-s` (default taken: the user asked for the log explicitly).
- `touches` widened to the new ADR file and `Documentation/Planning/Decisions/README.md` to record
  those decisions; no task in `Doing` names either.
- Config files: `Curl.Cli` reports no list of the config files it read, so the runner wraps the
  parse's `IDataFileReader` (`RecordingDataFileReader`) and logs every file the parse tried -
  `.curlrc` candidates, `-K` files and `@file` values - saying whether each was read. Parser
  warnings are logged just after the log opens, since it does not exist while they print.
- `ConnectTarget.DiagnosticLog`: the console builds no `ConnectTarget` itself (the handlers do, from
  `ITransferContext.DiagnosticLog`), so setting `TransferContext.DiagnosticLog` covers it. No
  service in the composition takes an `IDiagnosticLog` constructor argument yet; BL-920 onward add
  them.
- End line bytes are `TransferResult.BytesTransferred`. The loopback tests use a temporary directory
  through `PhysicalFileSystem`, as `CurlCompositionImapTests` do, and run in the fast suite.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; every fast test assembly passes
  (Curl.Console.UnitTests 1720 passed, 13 skipped); `Measure-CodeQuality.ps1 -Library Curl.Console`
  100% line, 100% branch, 0 failing members, worst CRAP 10; `dotnet publish Curl.Console` exit 0
  with no warning, and the native binary writes the log lines to standard error and to `--log-file`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Curl.Console opens the run's diagnostic log from --log-level/--log-file, puts it on every transfer context, and logs the command line and each transfer's start, failure and end

---
id: BL-919
title: Compose the diagnostic log in Curl.Console from --log-level and --log-file and log the runner's decisions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-917, BL-918]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
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

- [ ] `Curl.Console.UnitTests` show that for a loopback HTTP transfer with `-v`, the standard output and standard error bytes of a run with `--log-level none` equal those of the same run without the option, and no log file is created when `--log-file` is combined with `--log-level none`.
- [ ] A run with `--log-level verbose --log-file <temp>/x.log` leaves standard output and standard error byte-identical to the run without it, and `x.log` holds lines matching `^\[\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z\] \[(error|warning|info|verbose)\] \[[a-z0-9]+\] ` including the runner's transfer end line with the exit code.
- [ ] A run with `--log-level info` and no `--log-file` writes the log lines to standard error (and to the `--stderr` file when `--stderr` is given), and `-s` does not suppress them.
- [ ] A run with `-u user:s3cret --log-level verbose` writes no line containing `s3cret`.
- [ ] An unopenable `--log-file` writes the warning once and the exit code equals the run without the option.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Console` and no failing member.
- [ ] `dotnet publish Curl.Console` (native AOT) still succeeds with no new trim or AOT warning.

## Notes

## Log

- 2026-09-29: Created.

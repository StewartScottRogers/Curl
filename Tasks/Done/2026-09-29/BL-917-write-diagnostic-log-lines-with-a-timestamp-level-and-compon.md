---
id: BL-917
title: Write diagnostic log lines with a timestamp, level and component through DiagnosticLogWriter in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-917 — Write diagnostic log lines with a timestamp, level and component through DiagnosticLogWriter in Curl.Output

## Goal

`DiagnosticLogWriter` in `Curl.Output.UnitLibrary` implements `IDiagnosticLog`: it keeps the lines at or below its configured level and writes each as `[<UTC timestamp>] [<level>] [<component>] <message><line end>` to a `TextWriter` it is given, one whole line per call, safely from parallel transfers.

## Context

- The format and rules are BL-937's ADR (decisions 2, 3, 5, 8); the contract is BL-938's `IDiagnosticLog`.
- Constructor: `DiagnosticLogWriter(TextWriter target, DiagnosticLogLevel level, TimeProvider timeProvider, string lineEnd)`. The console (BL-919) passes the standard-error writer or the `--log-file` writer, and the line end it already uses for curl's own standard-error text.
- Timestamp: `timeProvider.GetUtcNow()` formatted `yyyy-MM-ddTHH:mm:ss.fffZ` with `CultureInfo.InvariantCulture`. Level text: `error`, `warning`, `info`, `verbose`.
- `IsEnabled(level)` is `level != None && level <= configured`; `Write` of a disabled level writes nothing. Constructing it with `DiagnosticLogLevel.None` is refused with `ArgumentOutOfRangeException` (none means no writer at all; the console uses `NoDiagnosticLog.Instance`).
- A message containing CR or LF is written on one line with each CR written as `\r` and each LF as `\n` (two characters), so one call is always one line.
- Thread safety: a `lock` around format-and-write, flushing after each line so a crash keeps the log up to the failure. An `IOException` from the target is swallowed after the first one and the writer stops writing (the log must never change a transfer's exit code).
- Tests use `FakeTimeProvider`-style hand-rolled time (the solution has no `Microsoft.Extensions.TimeProvider.Testing` package; write a small `TimeProvider` subclass in the test project) and a `StringWriter`. Tests must be platform-neutral: pass the line end explicitly, never assert `Environment.NewLine`.

## Acceptance criteria

- [x] `Curl.Output.UnitLibrary/DiagnosticLogWriter.cs` exists and implements `IDiagnosticLog` as above.
- [x] `DiagnosticLogWriterTests` pin: `Write(Info, "http", "reply 200")` at 2026-09-29T14:03:07.123Z writes exactly `[2026-09-29T14:03:07.123Z] [info] [http] reply 200` plus the given line end; each level's `IsEnabled` at each configured level (a 5x4 table); a disabled level writes nothing; CR and LF in a message are escaped; `None` in the constructor throws; a target that throws `IOException` does not throw out of `Write` and later writes are skipped; two threads writing 1000 lines each produce 2000 whole lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly: the task and ADR-0222 fully specify the class, so no separate plan stage. One class, 19 tests.
- Level validation: the constructor refuses every value outside Error..Verbose, not only None, so the level-text lookup (indexed by level - 1) has no unreachable branch. `IsEnabled` is `level > None && level <= configured`, so an undefined negative level counts as disabled.
- A null `target`, `timeProvider` or `lineEnd` throws `ArgumentNullException`.
- Tests reuse the project's existing `FixedTimeProvider`. One extra test pins that a non-UTC offset is written in UTC.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` shows 100% line, 100% branch, 0 failing members; Curl.Output.UnitTests 467 passed; full fast suite green.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. DiagnosticLogWriter writes timestamped, levelled, component-tagged diagnostic lines, thread-safe and IOException-proof

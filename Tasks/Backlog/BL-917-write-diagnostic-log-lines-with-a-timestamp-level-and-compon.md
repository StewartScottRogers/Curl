---
id: BL-917
title: Write diagnostic log lines with a timestamp, level and component through DiagnosticLogWriter in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-916]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-917 — Write diagnostic log lines with a timestamp, level and component through DiagnosticLogWriter in Curl.Output

## Goal

`DiagnosticLogWriter` in `Curl.Output.UnitLibrary` implements `IDiagnosticLog`: it keeps the lines at or below its configured level and writes each as `[<UTC timestamp>] [<level>] [<component>] <message><line end>` to a `TextWriter` it is given, one whole line per call, safely from parallel transfers.

## Context

- The format and rules are BL-915's ADR (decisions 2, 3, 5, 8); the contract is BL-916's `IDiagnosticLog`.
- Constructor: `DiagnosticLogWriter(TextWriter target, DiagnosticLogLevel level, TimeProvider timeProvider, string lineEnd)`. The console (BL-919) passes the standard-error writer or the `--log-file` writer, and the line end it already uses for curl's own standard-error text.
- Timestamp: `timeProvider.GetUtcNow()` formatted `yyyy-MM-ddTHH:mm:ss.fffZ` with `CultureInfo.InvariantCulture`. Level text: `error`, `warning`, `info`, `verbose`.
- `IsEnabled(level)` is `level != None && level <= configured`; `Write` of a disabled level writes nothing. Constructing it with `DiagnosticLogLevel.None` is refused with `ArgumentOutOfRangeException` (none means no writer at all; the console uses `NoDiagnosticLog.Instance`).
- A message containing CR or LF is written on one line with each CR written as `\r` and each LF as `\n` (two characters), so one call is always one line.
- Thread safety: a `lock` around format-and-write, flushing after each line so a crash keeps the log up to the failure. An `IOException` from the target is swallowed after the first one and the writer stops writing (the log must never change a transfer's exit code).
- Tests use `FakeTimeProvider`-style hand-rolled time (the solution has no `Microsoft.Extensions.TimeProvider.Testing` package; write a small `TimeProvider` subclass in the test project) and a `StringWriter`. Tests must be platform-neutral: pass the line end explicitly, never assert `Environment.NewLine`.

## Acceptance criteria

- [ ] `Curl.Output.UnitLibrary/DiagnosticLogWriter.cs` exists and implements `IDiagnosticLog` as above.
- [ ] `DiagnosticLogWriterTests` pin: `Write(Info, "http", "reply 200")` at 2026-09-29T14:03:07.123Z writes exactly `[2026-09-29T14:03:07.123Z] [info] [http] reply 200` plus the given line end; each level's `IsEnabled` at each configured level (a 5x4 table); a disabled level writes nothing; CR and LF in a message are escaped; `None` in the constructor throws; a target that throws `IOException` does not throw out of `Write` and later writes are skipped; two threads writing 1000 lines each produce 2000 whole lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.

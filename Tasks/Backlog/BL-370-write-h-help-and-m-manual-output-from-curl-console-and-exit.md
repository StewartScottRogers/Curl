---
id: BL-370
title: Write -h/--help and -M/--manual output from Curl.Console and exit 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-370 — Write -h/--help and -M/--manual output from Curl.Console and exit 0

## Goal

`curl -h`, `curl --help <subject>` and `curl -M` run through `Curl.Console` write the bytes curl 8.21.0 writes and exit 0.

## Context

- BL-201 made `CommandLineParser` accept `-h`/`--help` and `-M`/`--manual` (parsing ends there, as for `-V`) and set `CommandLineOptions.HelpRequested`, `HelpSubject` and `ManualRequested`; `CurlHelpText.Lines(subject, columns)` and `CurlManual.Lines()` give the lines. Until this task lands, `CurlCommandRunner` treats such a command line as accepted with no URL.
- Mirror the `VersionRequested` branch in `CurlCommandRunner`: write the lines to standard output with the platform newline (CRLF on Windows, as the mingw reference writes), and exit 0. The help columns come from `TerminalColumns.Resolve()`, which already follows curl's `get_terminal_columns`.
- An option subject (`CurlHelpText.IsOptionSubject(subject)` is true, e.g. `--help -v`) is printed by BL-368's code. If BL-368 has not landed, write nothing for it, exit 0, and record that gap in Notes; do not call `CurlHelpText.Lines` for it, which throws.
- ADR-0066 records the design.

## Acceptance criteria

- [ ] A `CurlCommandRunner` test shows `-h` writes `CurlHelpText.Lines(null, columns)` joined with the platform newline to standard output, nothing to standard error, and returns 0.
- [ ] A test shows `--help all` and `--help http` write `CurlHelpText.Lines` for that subject at the resolved columns, and exit 0.
- [ ] A test shows `-M` writes `CurlManual.Lines()` and exits 0, and `-V -M` writes the version only.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.

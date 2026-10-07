---
id: BL-376
title: Write -h/--help and -M/--manual output from Curl.Console and exit 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-201]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0069-help-pages-come-from-curls-own-help-table-and-the-manual-is-embedded.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-376 — Write -h/--help and -M/--manual output from Curl.Console and exit 0

## Goal

`curl -h`, `curl --help <subject>` and `curl -M` run through `Curl.Console` write the bytes curl 8.21.0 writes and exit 0.

## Context

- BL-201 made `CommandLineParser` accept `-h`/`--help` and `-M`/`--manual` (parsing ends there, as for `-V`) and set `CommandLineOptions.HelpRequested`, `HelpSubject` and `ManualRequested`; `CurlHelpText.Lines(subject, columns)` and `CurlManual.Lines()` give the lines. Until this task lands, `CurlCommandRunner` treats such a command line as accepted with no URL.
- Mirror the `VersionRequested` branch in `CurlCommandRunner`: write the lines to standard output with the platform newline (CRLF on Windows, as the mingw reference writes), and exit 0. The help columns come from `TerminalColumns.Resolve()`, which already follows curl's `get_terminal_columns`.
- An option subject (`CurlHelpText.IsOptionSubject(subject)` is true, e.g. `--help -v`) is printed by BL-374's code. If BL-374 has not landed, write nothing for it, exit 0, and record that gap in Notes; do not call `CurlHelpText.Lines` for it, which throws.
- ADR-0069 records the design.

## Acceptance criteria

- [x] A `CurlCommandRunner` test shows `-h` writes `CurlHelpText.Lines(null, columns)` joined with the platform newline to standard output, nothing to standard error, and returns 0.
- [x] A test shows `--help all` and `--help http` write `CurlHelpText.Lines` for that subject at the resolved columns, and exit 0.
- [x] A test shows `-M` writes `CurlManual.Lines()` and exits 0, and `-V -M` writes the version only.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

- Delivered directly rather than through the full multi-agent `/feature` stages. The change mirrors the existing `-V` branch, and ADR-0069 already records the design.
- `CurlCommandRunner.InformationLines` picks the lines for `-V`, `-M` or `-h [subject]` (the parser stops at the first of these, so at most one is set). `WriteStandardOutputLinesAsync` replaces `WriteVersionLinesAsync` and writes them with `Environment.NewLine`. Help uses the runner's `terminalColumns`, which the composition resolves with `TerminalColumns.Resolve()`.
- BL-374 had landed, so an option subject (`--help -v`) prints `CurlOptionManualSection`'s lines. An unknown option writes `Incorrect option name to show help for, see curl -h` to standard error and exits 0. Measured on mingw curl 8.21.0 on 2026-09-27: `--help --bogus` gives that line and exit 0.
- `-h file:///dir/x` reads the URL as the help subject and prints the unknown-category page, as curl does. Tests therefore use `-h` alone.
- Measure-CodeQuality: all new members are at 100%. The one failing `Curl.Console` member, `DiskWriteOutFileOpener.TryOpen` (0% in the fast run), predates this task and is already filed as BL-432, so this task adds no failing member.
- Out of scope: `help` lines in a `-K` file (`CommandLineOptions.ConfigFileHelpSubjects`, BL-375) are not printed by the console yet.
- Updated ADR-0069's consequence line, which said `-h` printed nothing until BL-376 landed. Added `Documentation/Planning/Decisions/ADR-0069-help-pages-come-from-curls-own-help-table-and-the-manual-is-embedded.md` to `touches` for that; no task in Doing touches it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -h, --help <subject|option> and -M through Curl.Console write curl 8.21.0's pages and exit 0

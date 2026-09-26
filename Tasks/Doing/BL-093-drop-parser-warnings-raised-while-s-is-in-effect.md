---
id: BL-093
title: Drop parser warnings raised while -s is in effect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-051]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-093 — Drop parser warnings raised while -s is in effect

## Goal

`CommandLineParser` leaves out of `CommandLineParseResult.WarningLines` every warning raised
while `-s`/`--silent` is in effect at that point of the command line, matching curl 8.21.0,
on both accepted and refused results.

## Context

- curl's `warnf` checks the global silent flag when the warning is raised during parsing,
  so the order of the arguments decides. Measured with curl 8.21.0
  (x86_64-w64-mingw32, Schannel) on Windows, 2026-09-26, stderr captured separately:
  - `curl -s -o -x file:///C:/Windows/win.ini` - no warning, exit 0.
  - `curl -sS -o -x file:///C:/Windows/win.ini` - no warning, exit 0 (`-S` does not bring it back).
  - `curl -o -x file:///C:/Windows/win.ini` - `Warning: The filename argument '-x' looks like a flag.` then the progress meter, exit 0.
  - `curl -o -x -s file:///C:/Windows/win.ini` - the warning IS printed (`-s` came after it).
  - `curl -s -o -x --bogus` - no warning; `curl: option --bogus: is unknown`, the try-help line, exit 2.
  - `curl -o -x -s --bogus` - the warning, then the unknown-option and try-help lines, exit 2.
  - `curl -s -o -x --no-silent file:///...` - no warning (it was dropped when raised), progress meter shown.
- The warning is added in `CommandLineOption.FileName` (`Curl.Cli.UnitLibrary/CommandLineOption.cs`)
  via `CommandLineOptions.AddWarningLine`; `CommandLineOptions.Silent` holds the flag. The
  natural seam is `AddWarningLine` ignoring the line while `Silent` is true.
- BL-088 (console prints `WarningLines`) is blocked on this: once the parser filters, the
  console prints the list unconditionally.

## Acceptance criteria

- [ ] A `Curl.Cli.UnitTests` test parses `-s -o -x file:///x`: accepted, `WarningLines` is empty.
- [ ] A test parses `-sS -o -x file:///x`: `WarningLines` is empty.
- [ ] A test parses `-o -x -s file:///x`: `WarningLines` is exactly the flag-like warning for `-x`.
- [ ] A test parses `-s -o -x --bogus`: refused, `WarningLines` is empty; and `-o -x -s --bogus`: refused, `WarningLines` holds the warning.
- [ ] A test parses `-s -o -x --no-silent file:///x`: `WarningLines` is empty.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green, with 100% line and branch coverage kept.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.

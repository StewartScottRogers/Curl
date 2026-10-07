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
completed: 2026-09-26
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

- [x] A `Curl.Cli.UnitTests` test parses `-s -o -x file:///x`: accepted, `WarningLines` is empty.
- [x] A test parses `-sS -o -x file:///x`: `WarningLines` is empty.
- [x] A test parses `-o -x -s file:///x`: `WarningLines` is exactly the flag-like warning for `-x`.
- [x] A test parses `-s -o -x --bogus`: refused, `WarningLines` is empty; and `-o -x -s --bogus`: refused, `WarningLines` holds the warning.
- [x] A test parses `-s -o -x --no-silent file:///x`: `WarningLines` is empty.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green, with 100% line and branch coverage kept.

## Notes

- Delivered in-session rather than through the full `/feature` agent chain: the change is one
  line of production code at the seam the Context names, so a separate plan would add nothing.
- Seam: `CommandLineOption.FileName` now calls the existing
  `CommandLineOptions.AddWarningLinesUnlessSilent` (added for the `-r` range warnings), and the
  unfiltered `AddWarningLine` was removed, so no parser warning can bypass `-s`. One concept, one
  name: every parser warning now goes through the silent check.
- Tests (`CommandLineParserTests`): `Parse_FlagLikeOutputFileAfterSilent_IsAcceptedWithoutWarning`
  (`-s`, `-sS`), `Parse_FlagLikeOutputFileBeforeSilent_KeepsTheWarning`,
  `Parse_FlagLikeOutputFileAfterSilentThenUnknownOption_RefusesWithoutWarning`,
  `Parse_FlagLikeOutputFileBeforeSilentThenUnknownOption_RefusesAndKeepsTheWarning`,
  `Parse_FlagLikeOutputFileWhileSilentThenNoSilent_StaysWithoutWarning`.
- `--no-silent` is not parsed yet (`--no-` negation is BL-054), so `-s -o -x --no-silent file:///x`
  is currently refused as unknown. The criterion asks only that `WarningLines` be empty, which
  holds; the test asserts that alone so it keeps passing once BL-054 lands. No new task filed:
  BL-054 already covers negation.
- Curl.Cli.UnitLibrary coverage after the change: 100% line, 100% branch. 441 Cli tests pass.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Parser drops warnings raised while -s is in effect, on accepted and refused results, matching curl 8.21.0

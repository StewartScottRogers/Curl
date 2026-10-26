---
id: BL-3845
title: CGR-015 part 6: --variable content and --expand- values carry command-line text in the ANSI code page on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3844]
touches: [Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Cli.UnitLibrary/VariableExpansion.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitTests/Cgr015VariableTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3845 — CGR-015 part 6: --variable content and --expand- values carry command-line text in the ANSI code page on Windows

## Goal

On Windows, `--variable v=é --expand-data {{v}}` posts E9 (the ANSI code page byte, as real curl does), while a variable read from a file (`--variable v@file`) still carries the file's bytes unchanged.

## Context

Follow-up of BL-3844 (CGR-015 part 2, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; BL-3544 holds the decision: on Windows command-line text goes out in the system ANSI code page). BL-3844 made `-d`, `--json`, `--data-urlencode` and `--url-query` send typed text through `CommandLineOptions.CommandLineTextBytes` (the ANSI code page held in `ConfigFileWireTextEncoding` on Windows), but left any `--expand-` value whose references were replaced as UTF-8 (`CommandLineOptions.ApplyingExpandedValue`, set in `CommandLineParser.ApplyExpandedValue`), because `VariableDefinition` stores `=content` as UTF-8 bytes (`VariableDefinition.ApplyContent`) and `VariableExpansion.Expand` appends typed text as UTF-8 and decodes the result as UTF-8. To match curl: store `=content` typed on the command line in the ANSI code page, append the template's typed text in it, and hand the -d family the expanded bytes unchanged; keep the UTF-8 reading the leading-Unicode check needs (`ApplyingValueLedByVariableBytes`, BL-1814, upstream test268). Then drop `ApplyingExpandedValue` if nothing else needs it. Measure real curl with `Record-CurlExchange.ps1` first (`--variable v=é --expand-data {{v}}`, `--variable v@file` with UTF-8 bytes, `--expand-url-query`).

## Acceptance criteria

- [ ] With cp1252, `--variable v=é --expand-data {{v}}` posts E9 and `--variable v@f.txt --expand-data {{v}}` with f.txt holding C3 A9 posts C3 A9, as real curl on Windows does (commands and results in Notes).
- [ ] Focused unit tests in `Curl.Cli.UnitTests/Cgr015VariableTests.cs` pin both, and off Windows the same commands send UTF-8 as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for Curl.Cli.UnitLibrary shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created.

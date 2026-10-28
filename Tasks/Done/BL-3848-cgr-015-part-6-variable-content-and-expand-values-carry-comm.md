---
id: BL-3848
title: CGR-015 part 6: --variable content and --expand- values carry command-line text in the ANSI code page on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3844]
touches: [Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Cli.UnitLibrary/VariableExpansion.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitTests/Cgr015VariableTests.cs, Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3848 — CGR-015 part 6: --variable content and --expand- values carry command-line text in the ANSI code page on Windows

## Goal

On Windows, `--variable v=é --expand-data {{v}}` posts E9 (the ANSI code page byte, as real curl does), while a variable read from a file (`--variable v@file`) still carries the file's bytes unchanged.

## Context

Follow-up of BL-3844 (CGR-015 part 2, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; BL-3544 holds the decision: on Windows command-line text goes out in the system ANSI code page). BL-3844 made `-d`, `--json`, `--data-urlencode` and `--url-query` send typed text through `CommandLineOptions.CommandLineTextBytes` (the ANSI code page held in `ConfigFileWireTextEncoding` on Windows), but left any `--expand-` value whose references were replaced as UTF-8 (`CommandLineOptions.ApplyingExpandedValue`, set in `CommandLineParser.ApplyExpandedValue`), because `VariableDefinition` stores `=content` as UTF-8 bytes (`VariableDefinition.ApplyContent`) and `VariableExpansion.Expand` appends typed text as UTF-8 and decodes the result as UTF-8. To match curl: store `=content` typed on the command line in the ANSI code page, append the template's typed text in it, and hand the -d family the expanded bytes unchanged; keep the UTF-8 reading the leading-Unicode check needs (`ApplyingValueLedByVariableBytes`, BL-1814, upstream test268). Then drop `ApplyingExpandedValue` if nothing else needs it. Measure real curl with `Record-CurlExchange.ps1` first (`--variable v=é --expand-data {{v}}`, `--variable v@file` with UTF-8 bytes, `--expand-url-query`).

## Acceptance criteria

- [x] With cp1252, `--variable v=é --expand-data {{v}}` posts E9 and `--variable v@f.txt --expand-data {{v}}` with f.txt holding C3 A9 posts C3 A9, as real curl on Windows does (commands and results in Notes).
- [x] Focused unit tests in `Curl.Cli.UnitTests/Cgr015VariableTests.cs` pin both, and off Windows the same commands send UTF-8 as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for Curl.Cli.UnitLibrary shows no member outside a gate.

## Notes

- Measured real curl 8.21.0 (C:\windows\system32\curl.exe, Schannel, ACP 1252) on 2026-10-28 with Record-CurlExchange.ps1: `--variable v=é --expand-data {{v}}` posts E9; `--variable v@f.txt --expand-data {{v}}` (f.txt = C3 A9) posts C3 A9; `--expand-data é{{v}}é` posts E9 E9 E9; `--expand-url-query q={{v}}` sends `/?q=%E9`; `--variable %V` with V=é posts E9; `--expand-header "X-A: é{{v}}"` with the file sends X-A: E9 C3 A9.
- Decision (ADR-0934): `=content` is stored through `CommandLineTextBytes`, an import in the ANSI code page on Windows, typed template text is appended through `CommandLineTextBytes`, and the expanded bytes are read back with its new inverse `CommandLineOptions.CommandLineText`, so every option (not only the -d family) sends the bytes unchanged; `ApplyingExpandedValue` is dropped. The leading-Unicode check reads a variable-led value' bytes as UTF-8 through `CommandLineOptions.ValueReadForLeadingUnicodeCheck`.
- Touches widened to CommandLineOption.cs (the leading-Unicode check must now read the value's bytes as UTF-8) and CommandLineGlobalState.cs (holds `ApplyingExpandedValue`, removed); no task in Doing on origin/work/dark-factory names either.
- Tests: Curl.Cli.UnitTests/Cgr015VariableTests.cs pins the Windows (cp1252) and off-Windows bytes; fast tests are platform-neutral (the Windows cases pass isWindows to the parser), which meets the off-Windows criterion here.
- Gates: dotnet build clean, fast tests green, Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: db5eac33 16c33551 c463b44c.
- 2026-10-28: Doing -> Done. --variable =content, imports and --expand- values carry command-line text in the ANSI code page on Windows; file bytes go out unchanged

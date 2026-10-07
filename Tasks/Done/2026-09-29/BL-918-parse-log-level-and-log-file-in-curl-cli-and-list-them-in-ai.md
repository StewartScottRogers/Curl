---
id: BL-918
title: Parse --log-level and --log-file in Curl.Cli and list them in --ai-help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-937, BL-911]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-918 — Parse --log-level and --log-file in Curl.Cli and list them in --ai-help

## Goal

`Curl.Cli.UnitLibrary` parses `--log-level <none|error|warning|info|verbose>` and `--log-file <path>` into `CommandLineOptions.DiagnosticLogLevel` and `CommandLineOptions.DiagnosticLogFile`, refuses a bad level with exit 2, and lists both options in `--ai-help`, while `--help` and `--manual` stay byte-identical to curl 8.21.0.

## Context

- Rules: BL-937's ADR, decision 1 (and 10 for help output). Types: BL-938's `DiagnosticLogLevel` in `Curl.Protocol.Abstractions` (Curl.Cli already references it).
- Option table: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (the `CommandLineOption.Value(...)`/`FileName(...)` entries near `--stderr`, line ~77). Both options are global: store them on the run-wide `globals` object in `CommandLineOptions.cs` as `StandardErrorFile` is (line ~271), so `-:`/`--next` does not reset them; the last occurrence wins.
- Values: `none`, `error`, `warning`, `info`, `verbose`, compared with `StringComparison.OrdinalIgnoreCase`. Anything else (empty included) is refused as badly used, the same way other badly used options are (`curl: option --log-level: is badly used here`, then the `curl: try 'curl --help' or 'curl --manual' for more information` line), exit 2 (`CurlExitCode.FailedInit`).
- `--log-file <path>` with no `--log-level` anywhere on the line makes the effective level `Info`; an explicit `--log-level` (before or after it) wins. Expose the effective level as `CommandLineOptions.DiagnosticLogLevel` (default `DiagnosticLogLevel.None`) and the path as `CommandLineOptions.DiagnosticLogFile` (`string?`, default `null`). Parsing never opens the file; the console does (BL-919).
- `--ai-help` (BL-911, a dependency) generates its option reference from the option table and has a unit test that fails when an option has no category. Give `--log-level` and `--log-file` the category BL-911 gave Curl-only options such as `--ai-help` itself, with a description saying they are Curl's own diagnostic log, not curl's `-v`, and naming the levels. `CurlHelpText`, `CurlHelpTable` and `CurlManual.txt` must not gain them.
- Both options also work from a `-K` config file (`log-level = verbose`), because config files use the same table; pin that with one test.

## Acceptance criteria

- [x] `CommandLineOptionTable` has `log-level` and `log-file`; `CommandLineOptions.DiagnosticLogLevel` and `DiagnosticLogFile` exist with the defaults above.
- [x] `Curl.Cli.UnitTests` pin: each of the five levels in lower and mixed case; the default `None`; `--log-level bogus` and `--log-level ""` refused with the badly-used text and exit 2; `--log-file x.log` alone gives `Info`; `--log-file x.log --log-level error` and `--log-level error --log-file x.log` give `Error`; last occurrence wins; both survive `--next`; `log-level = verbose` in a `-K` file.
- [x] `curl --ai-help all` lists `--log-level` and `--log-file` with their descriptions, and BL-911's every-option-has-a-category test passes.
- [x] `curl --help all` and `curl --manual` output is unchanged byte for byte (existing help and manual tests still pass unmodified).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: two table rows beside `--stderr` (`log-level` a `Value` row with its own applier over a case-insensitive name-to-level `FrozenDictionary`, so a number such as `3` is refused, which `Enum.TryParse` would have accepted; `log-file` a `FileName` row, so an empty name is refused as blank and a flag-like name warns, as for `--trace`). Both are in `GlobalOptionLongNames` and stored on `CommandLineGlobalState`; `DiagnosticLogLevel` is computed as given level ?? (file ? Info : None).
- `--ai-help`: `CurlAiHelpText` now appends a `CurlOnlyEntries` array (`--ai-help`, `--log-level`, `--log-file`, category `curl`) to `CurlHelpTable.Entries`; `CurlHelpTable`, `CurlHelpText` and `CurlManual.txt` are untouched, so `--help` and `--manual` bytes are unchanged (their tests pass unmodified).
- `--no-log-level` / `--no-log-file` are refused as not reversible, like every value option.
- Measure-CodeQuality flagged BL-911's `CurlManualMarkdown.Escape` at complexity 14 (inside this task's touches); it now tests membership with `SearchValues` and the library reports 0 failing members, worst CRAP 10.
- No ADR needed: every behaviour here is ADR-0222 decisions 1 and 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --log-level and --log-file parse into CommandLineOptions.DiagnosticLogLevel/DiagnosticLogFile and are listed in --ai-help

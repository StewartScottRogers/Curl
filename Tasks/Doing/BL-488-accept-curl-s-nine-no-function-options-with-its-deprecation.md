---
id: BL-488
title: Accept curl's nine no-function options with its deprecation warning and exit 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-488 — Accept curl's nine no-function options with its deprecation warning and exit 0

## Goal

`--sslv2` (`-2`), `--sslv3` (`-3`), `--metalink`, `--npn`, `--ntlm-wb`, `--egd-file <file>`, `--random-file <file>`, `--krb4 <level>` and `--false-start` are accepted as curl 8.21.0 accepts them: a warning line on standard error, no effect, and the transfer carries on (exit 0 when it succeeds), instead of today's `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 1 (Blocker, measured): curl 8.21.0 (Schannel, Windows) prints `Warning: --X is deprecated and has no function anymore` and exits 0; Curl refuses each as unknown with exit 2.
- All nine are rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` (copied from `tool_getparam.c` at `curl-8_21_0`) and in `CurlHelpTable.cs` under the `deprecated` category, but have no row in `CommandLineOptionTable.cs`, so `CommandLineParser` returns `CommandLineRefusal.UnknownOption`.
- Warnings are built in `Curl.Cli.UnitLibrary/CommandLineWarning.cs` and written by the console; follow how the existing warnings (e.g. `FileNameLooksLikeFlag`) travel from the parser to standard error.
- `--egd-file`, `--random-file` and `--krb4` take a value; the other six are flags. `CurlOptionAliasTable` says which take `--no-` (`--metalink`, `--npn`, `--ntlm-wb`, `--false-start` accept it).
- Manual: `Curl.Cli.UnitLibrary/CurlManual.txt` (8.21.0) and https://curl.se/docs/manpage.html (now 8.23.0; the 8.21.0 text is the one embedded).

## Acceptance criteria

- [ ] Measured first: the reference curl 8.21.0 run through `Record-CurlExchange.ps1` against a loopback 200 for each of the nine, for the short forms `-2` and `-3`, for one `--no-` form (`--no-metalink`) and for each under `-s`, with stdout, stderr and exit code copied into Notes before any text is pinned.
- [ ] Each of the nine parses with no effect on `CommandLineOptions` and produces the measured warning line; a test in `Curl.Cli.UnitTests` covers every name (data rows), the short forms and the value-taking forms consuming their value.
- [ ] A `Curl.Console.UnitTests` test runs `curl --metalink file:///dir/x`-style command lines through the runner against a fake handler and pins the measured standard-error bytes and exit code, including the `-s` case.
- [ ] `--bogus` is still refused as `is unknown`, exit 2.
- [ ] New tests are platform-neutral (no drive-letter paths or Windows-only text outside `[OSCondition(OperatingSystems.Windows)]`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.

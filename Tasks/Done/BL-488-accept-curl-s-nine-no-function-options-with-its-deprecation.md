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
completed: 2026-09-28
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

- [x] Measured first: the reference curl 8.21.0 run through `Record-CurlExchange.ps1` against a loopback 200 for each of the nine, for the short forms `-2` and `-3`, for one `--no-` form (`--no-metalink`) and for each under `-s`, with stdout, stderr and exit code copied into Notes before any text is pinned.
- [x] Each of the nine parses with no effect on `CommandLineOptions` and produces the measured warning line; a test in `Curl.Cli.UnitTests` covers every name (data rows), the short forms and the value-taking forms consuming their value.
- [x] A `Curl.Console.UnitTests` test runs `curl --metalink file:///dir/x`-style command lines through the runner against a fake handler and pins the measured standard-error bytes and exit code, including the `-s` case.
- [x] `--bogus` is still refused as `is unknown`, exit 2.
- [x] New tests are platform-neutral (no drive-letter paths or Windows-only text outside `[OSCondition(OperatingSystems.Windows)]`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1` (curl 8.21.0, Schannel, Windows) against a loopback
`HTTP/1.1 200 OK` with body `ok`, URL `http://127.0.0.1:18488/`:

| Arguments before the URL | Exit | stdout | stderr |
| --- | ---: | --- | --- |
| `--sslv2`, `-2` | 0 | `ok` | `Warning: --sslv2 is deprecated and has no function anymore` then the progress meter |
| `--sslv3`, `-3` | 0 | `ok` | `Warning: --sslv3 is deprecated and has no function anymore` then the meter |
| `--metalink`, `--no-metalink` | 0 | `ok` | `Warning: --metalink is deprecated and has no function anymore` then the meter |
| `--npn`, `--no-npn` | 0 | `ok` | `Warning: --npn is deprecated ...` then the meter |
| `--ntlm-wb`, `--no-ntlm-wb` | 0 | `ok` | `Warning: --ntlm-wb is deprecated ...` then the meter |
| `--false-start`, `--no-false-start` | 0 | `ok` | `Warning: --false-start is deprecated ...` then the meter |
| `--egd-file x`, `--egd-file=x` | 0 | `ok` | `Warning: --egd-file is deprecated ...` then the meter |
| `--random-file x` | 0 | `ok` | `Warning: --random-file is deprecated ...` then the meter |
| `--krb4 x`, `--krb4 ""` | 0 | `ok` | `Warning: --krb4 is deprecated ...` then the meter |
| `-s --metalink`, `-s --sslv2`, `-s -2`, `-s --egd-file x` | 0 | `ok` | (empty) |
| `--metalink -s` | 0 | `ok` | `Warning: --metalink is deprecated and has no function anymore` only |
| `-2s`, `-2v`, `-3s`, `-23` | 0 | `ok` | the `-2`/`-3` warning only, then the meter: the rest of the bundle is ignored |
| `--egd-file` (URL taken as its value) | 2 | | warning, `curl: (2) no URL specified`, try-help |
| URL then `--egd-file` / `--krb4` last | 2 | | `curl: option --egd-file: requires parameter` (no warning), try-help |
| `--no-sslv2`, `--no-egd-file x` | 2 | | `... the given option cannot be reversed with a --no- prefix`, try-help |
| `--bogus` | 2 | | `curl: option --bogus: is unknown`, try-help |

Every stderr line ends CRLF. Delivered in the session rather than through the full agent pipeline: the
change is one library's rows plus a bundle rule, every text measured above. New
`CommandLineOption.NoFunctionFlag` / `NoFunctionValue` factories and `CommandLineOption.EndsBundle`
(a no-function letter ends its short-option bundle, matching the measured `-2s` / `-23`); the warning is
`CommandLineWarning.DeprecatedWithNoFunction`. No ADR: nothing was decided beyond matching the
measurement. Tests: `CommandLineNoFunctionOptionTests` (43) and `CurlCommandRunnerNoFunctionOptionTests`
(16). Fast suite green; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch,
0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl's nine no-function options (-2/-3, --metalink, --npn, --ntlm-wb, --false-start, --egd-file, --random-file, --krb4) warn as curl 8.21.0 does and the transfer carries on

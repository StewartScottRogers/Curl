---
id: BL-190
title: Parse -L, --max-redirs, --post30x, --location-trusted, -i, -I, -f and the fail options
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-190 — Parse -L, --max-redirs, --post30x, --location-trusted, -i, -I, -f and the fail options

## Goal

`-L`, `--max-redirs`, `--post301`, `--post302`, `--post303`, `--location-trusted`, `-i`, `-I`, `-f`, `--fail-with-body` and `--fail-early` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- None of these has a row in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` today (checked 2026-09-26). `-I` maps onto the existing `NoBody` transfer member when wired (BL-232).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `--max-redirs -1` means no limit; the default is 50.
- [x] The measured curl 8.21.0 result of combining `-f` and `--fail-with-body` is pinned in a test.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel): `curl <args> http://127.0.0.1:1/ -o /dev/null [--connect-timeout 0.1]`, reading standard error and the exit code. No loopback server was needed: every result here is decided while parsing (BL-165's recorder does not exist yet). Pinned in `Curl.Cli.UnitTests/CommandLineRedirectAndFailOptionTests.cs`.
  - `--max-redirs`: `-1`, `-01`, `-0`, `0`, `5`, `007`, `2147483647` accepted; `''`, `-2`, `--1`, `-`, `abc`, `5x`, ` 5`, `+5`, `0x10`, `1.5`, `2147483648`, `-2147483648`, `99999999999999999999` exit 2 with `curl: option --max-redirs: expected a proper numerical parameter` + try-help (never "too large" or "positive"). Last as argument: `requires parameter`. Read by the new `CommandLineNumber.ParseMinusOneOrMore`.
  - `-f --fail-with-body` prints `Warning: --fail-with-body deselects --fail here`; `--fail-with-body -f` prints `Warning: --fail deselects --fail-with-body here` (names `--fail` even for `-f`); the last wins; both exit per the transfer, not 2. Dropped when `-s` was read first (even with `-S`), kept when `-s` comes after. `--no-fail` and `--no-fail-with-body` each clear both modes (no warning when the other mode follows).
  - `-I --no-head` / `--no-head -I` (and `=x`, and `-Is`) print curl's two-line "You can only select one HTTP request method!" warning, then `curl: option <as typed>: is badly used here` + try-help, exit 2; the warning is dropped when `-s` was read first. `-I -I`, `--no-head --no-head`, `-i -I`, `-I -G`, `-I -X POST` are accepted.
  - `--no-` spellings: `--no-location`, `--no-location-trusted`, `--no-post301/302/303`, `--no-show-headers`, `--no-include`, `--no-head`, `--no-fail`, `--no-fail-with-body`, `--no-fail-early` are accepted (also with `=x`); `--no-max-redirs` (and `=x`) is refused as not reversible. `-i`'s long name in 8.21.0 is `--show-headers`; `--include` is kept as its alias (two rows, one letter).
- Decisions (sensible defaults, recorded here):
  - `FailMode` uses the existing `HttpFailMode` from `Curl.Protocol.Abstractions` (ADR-0014) rather than a new enum.
  - `--no-location-trusted` turns off both following and credential sending, and `-L`/`--no-location` leave the credential flag alone, as curl's tool source (`C_LOCATION_TRUSTED` falls through to `C_LOCATION`) does; this is state, not output text, so it could not be measured without a redirecting server.
  - `-I` conflicts needed a flag whose applier can refuse: `CommandLineOption.Negate` is now a `CommandLineOptionApplier` (was `Action<CommandLineOptions>`), the new `CommandLineOption.NegatableFlagThatCanRefuse` builds such a row, and the parser now checks a flag's result inside a bundle. `ParseShortBundle` was split (`ApplyRestOfBundle`) to stay at complexity 10 or less.
  - `-I` / `--no-head` combined with `-d`/`--json` (POST) is not refused here: curl 8.21.0 checks it in `tool_operate` at transfer setup, whatever the order, exiting 2 with the two warning lines only. Filed as BL-253 rather than widening this task.
- Quality: `Measure-CodeQuality.ps1 -Library "Curl.Cli*"` reports Curl.Cli.UnitLibrary 100% line, 100% branch, 335 members, 0 failing, worst CRAP 10. A first full run hit two `Curl.Networking` TLS test failures that pass alone and on rerun (load from parallel lanes, not this change).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -L, --location-trusted, --max-redirs, --post301/302/303, -i/--include, -I, -f, --fail-with-body and --fail-early parse into CommandLineOptions with curl 8.21.0's refusals, warnings and --no- spellings

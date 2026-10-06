---
id: BL-195
title: Parse -v, --trace, --trace-ascii, --trace-time and --stderr
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-195 — Parse -v, --trace, --trace-ascii, --trace-time and --stderr

## Goal

`-v`/`--verbose`, `--trace`, `--trace-ascii`, `--trace-time` and `--stderr` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C9. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The precedence between `-v`, `--trace` and `--trace-ascii` (last wins or otherwise) is measured on curl 8.21.0 and pinned, including any warning line.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C9 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel, Windows) as `curl <arguments> http://127.0.0.1:1/`, reading stderr, the exit code and which trace files appeared. Every case below exits 7 (connection refused) unless it says exit 2:
  - `-v --trace t1` and `-vv --trace t4` and `--trace-ascii t2 --trace t1`: `Warning: --trace overrides an earlier trace/verbose option`; only the last trace file is written.
  - `-v --trace-ascii t2`, `--trace t1 --trace-ascii t2`: `Warning: --trace-ascii overrides an earlier trace/verbose option`. `-v --trace-ascii t6 --trace t7`: both lines, in that order.
  - `--trace t1 -v`, `--trace-ascii t2 -v`, `--trace t1 --verbose`, `--trace t8 -v -v`: `Warning: -v, --verbose overrides an earlier trace option` once, then verbose lines; the trace file is not written.
  - No warning: `-v -v`, `--verbose --verbose`, `--trace t1 --trace t3` (only t3 written), `--trace-ascii t1 --trace-ascii t3`, `--trace t1 --no-verbose` (no file written), `--trace t1 --no-verbose -v`, `-v --no-verbose --trace t1`. With `-s` first (`-s -v --trace t5`, `-s --trace t5 -v`) the warning is dropped.
  - `-v --trace -x`: `Warning: The filename argument '-x' looks like a flag.` then the override line. `--trace -x`, `--stderr -x` warn the same.
  - Verbosity: `-vv` adds `HH:MM:SS.ffffff [0-0]` prefixes, `-vvv` adds `[READ]` lines, `-vvvv` adds `[MULTI]` lines, `-vvvvv` is the same as `-vvvv`. `-vv -v` and `-vv --verbose` and `-vv --no-verbose -v` print level 1; `-vv -sv` prints level 3; `-vsv` and `-svv` level 2. So a `v` that is the first option of its argument resets to level 1 (curl's `nopts == 0`), later letters add.
  - Times: `--trace-time -v` shows no times, `-v --trace-time` and `--trace-time -sv` do, `--trace-time --no-verbose -sv` does not, `-vv --no-trace-time` shows IDs without times.
  - Exit 2 with the try-help line: `--trace ''` / `--trace-ascii ''` (`curl: option --trace: blank argument where content is expected`), `--no-trace`, `--no-trace-ascii`, `--no-stderr` (`... the given option cannot be reversed with a --no- prefix`), and `--trace`, `--trace-ascii`, `--stderr` as the last argument (`... requires parameter`).
  - Accepted: `--no-verbose`, `--no-verbose=x`, `--verbose=x`, `--no-trace-time`, `--no-trace-time=x`, `--trace-time=x`.
  - `--stderr ''` is not refused: it prints `Warning: Warning: Failed to open ` and carries on to stderr. `--stderr se` sends everything curl writes to stderr afterwards, warnings included, to `se`; `--stderr -` sends it to stdout.
- Decisions (made under Stewart's delegation; recorded here and in the XML docs rather than a new ADR because `Documentation/Planning/Decisions` is in the `touches` of BL-303, now in Doing): the parser records `StandardErrorFile` and never opens it; opening it, the doubled `Warning: Warning: Failed to open <name>` line for a file that cannot be opened, and sending the warnings read after `--stderr` into that file are BL-242's (Curl.Console) job. Verbosity is kept as a count 0-4 on `CommandLineOptions.Verbosity`, with `TraceTime` separate, because that is how curl stores them; transfer and connection IDs are implied by `Verbosity >= 2` since `--trace-ids` is not in this task. To know whether a `v` is the first option of its argument the parser now sets `CommandLineOptions.FirstOptionOfArgument` before every option it applies, a generic signal rather than a special case for `-v`.
- Results: `dotnet build` clean (0 warnings); fast tests all green (Curl.Cli.UnitTests 1478 passed, 8 platform skips); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -v, --trace, --trace-ascii, --trace-time and --stderr parse into CommandLineOptions with curl 8.21.0's precedence, warnings and refusals

---
id: BL-517
title: Parse -Z/--parallel, --parallel-max, --parallel-immediate and --parallel-max-host
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-508]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-517 — Parse -Z/--parallel, --parallel-max, --parallel-immediate and --parallel-max-host

## Goal

`-Z`/`--parallel` (and `--no-parallel`), `--parallel-max <num>`, `--parallel-immediate` (and `--no-` form) and `--parallel-max-host <num>` parse into `CommandLineOptions` with curl 8.21.0's defaults, ranges and refusals, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker, L; split: this parse, BL-518 decision, BL-519 run, BL-520 limits, BL-521 meter).
- Alias-table rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`: `parallel` (`Z`, `--no-` accepted), `parallel-immediate` (`--no-` accepted), `parallel-max`, `parallel-max-host`. These are global options (they apply across `--next` groups), so this task waits for BL-508, which introduces the global/per-group split, and classifies all four as global.
- The manual (`CurlManual.txt`) gives `--parallel-max` default 50 and upper limit 300; what curl does with `0`, `301` and `-1` must be measured (clamped or refused).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--parallel-max 0`, `--parallel-max 301`, `--parallel-max -1`, `--parallel-max-host 0`, with `-Z` and a loopback URL; stderr and exit code copied into Notes.
- [x] Every spelling, the defaults and each measured edge case are covered by `Curl.Cli.UnitTests` data rows.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1 -Port 18517` against the local curl 8.21.0
(Schannel), arguments `<case> -s -S http://127.0.0.1:18517/`:

| Case | Exit | Standard error |
| --- | ---: | --- |
| `-Z --parallel-max 0` | 0 | (none) |
| `-Z --parallel-max 301` | 0 | (none) |
| `-Z --parallel-max 300` | 0 | (none) |
| `-Z --parallel-max -1` | 2 | `curl: option --parallel-max: expected a positive numerical parameter` + try-help line |
| `-Z --parallel-max-host 0` | 0 | (none) |
| `-Z --parallel-max-host 99999` | 0 | (none) |
| `-Z --parallel-max-host -1` | 2 | `curl: option --parallel-max-host: expected a positive numerical parameter` + try-help line |
| `-Z --parallel-max ""`, `abc`, `1.5`, `99999999999999999999`; `-Z --parallel-max-host ""` | 2 | `curl: option <opt>: expected a proper numerical parameter` + try-help line |
| `--no-parallel`, `--parallel-immediate`, `--no-parallel-immediate` | 0 | (none) |
| `--no-parallel-max 5`, `--no-parallel-max-host 5` | 2 | `curl: option <spelling>: the given option cannot be reversed with a --no- prefix` + try-help line |

- The parse reads both limits with `CommandLineNumber.ParseNonNegative` (curl's `str2unum`), so the
  refusals above come out byte for byte.
- Decision (sensible default, matching curl): accepted out-of-range values are not refused, so what
  curl does with them is internal. Following curl 8.21.0's manual ("The default is 50. 65535 is the
  largest supported value"; `--parallel-max-host` "default is 0 (unlimited). 65535 is the largest")
  and its tool's clamping in `tool_getparam.c`: `--parallel-max 0` means the default 50, a value past
  65535 means 65535; `--parallel-max-host 0` means no limit, past 65535 means 65535. The task's
  Context said 300 was the upper limit; the 8.21.0 manual says 65535, so 65535 it is. The effect of
  the clamp is only observable once transfers run concurrently, so BL-519/BL-520 should confirm it
  when they measure concurrency. No ADR: this matches curl rather than choosing between behaviours.
- All four are global (`CommandLineOptionTable.GlobalOptionLongNames`), held in
  `CommandLineGlobalState`; exposed as `CommandLineOptions.Parallel`, `ParallelImmediate`,
  `ParallelMax`, `ParallelMaxHost`.
- Tests: `Curl.Cli.UnitTests/CommandLineParallelOptionTests.cs`. Cli tests 2378 passed, 13 skipped
  (platform-conditional); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100%
  branch, 0 failing members, worst CRAP 10.
## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -Z/--parallel, --parallel-immediate, --parallel-max and --parallel-max-host parse as global options with curl 8.21.0's defaults, clamps and refusals

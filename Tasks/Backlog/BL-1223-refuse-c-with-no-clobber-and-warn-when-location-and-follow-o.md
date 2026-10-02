---
id: BL-1223
title: Refuse -C with --no-clobber and warn when --location and --follow override each other, as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1223 — Refuse -C with --no-clobber and warn when --location and --follow override each other, as curl does

## Goal

`CommandLineParser` refuses `-C`/`--continue-at` together with `--no-clobber` (exit 2, curl 8.21.0's three lines, naming whichever came second), and writes `Warning: --location overrides --follow` / `Warning: --follow overrides --location` when one of the two redirect options replaces the other, exactly as curl 8.21.0 does.

## Context

- Today `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` refuses `-C` with `-r` and with `--remove-on-error` (`ContinueAtExclusiveWithRange`, `ContinueAtExclusiveWithRemoveOnError` in `CommandLineRefusal.cs`, set up around lines 1240-1330), but `--clobber`/`--no-clobber` (line 90, a plain `NegatableFlag`) never checks `-C`, and `-C` never checks `--no-clobber`. `SetFollow` and `SetLocation`/`SetLocationTrusted` (lines 295-297, 1670-1700) write no warning.
- curl 8.21.0, `src/tool_getparam.c` at `curl-8_21_0`: `opt_continue_at` (lines 1202-1216) checks `--range`, then `--remove-on-error`, then `--no-clobber` (`errorf "--continue-at is mutually exclusive with --no-clobber"`, `PARAM_BAD_USE`); `C_CLOBBER` (lines 2173-2179) refuses `--no-clobber` after `-C` the same way. `C_LOCATION_TRUSTED` falls through to `C_LOCATION`, which warns `--location overrides --follow` when `--follow` was in force (lines 2216-2223); `C_FOLLOW` warns `--follow overrides --location` when `-L` was (lines 2224-2228). Both check the previous state before applying the toggle, so the `--no-` spellings warn too.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel):
  - `curl -C 5 --no-clobber -o x file:///nonexist`: `curl: --continue-at is mutually exclusive with --no-clobber` / `curl: option --no-clobber: is badly used here` / `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2.
  - `curl --no-clobber -C 5 -o x file:///nonexist`: the same, naming `-C`, exit 2.
  - `curl -L --follow file:///nonexist`: `Warning: --follow overrides --location`, then the transfer (`curl: (37) Could not open file /nonexist`).
  - `curl --follow -L file:///nonexist`: `Warning: --location overrides --follow`, then the transfer.
- The `-s`/`-S` rule for the error line is the one `ContinueAtExclusiveWithRange` documents; reuse it. Measure whether `-s` hides the two warnings before pinning that.

## Acceptance criteria

- [ ] Before the code change, `Notes` records curl 8.21.0's stderr and exit code for `-s -C 5 --no-clobber`, `-s -L --follow`, `--follow --location-trusted`, `-L --no-follow` and `--follow --no-location`.
- [ ] Tests in `Curl.Cli.UnitTests` pin every case above, measured and newly measured, line for line: the refusal's lines and exit 2, and each warning's text and place among the parser's warnings.
- [ ] No other refusal or warning changes; every existing Cli test passes unchanged.
- [ ] `curl --ai-help` needs no change (no option is added or changed); say so in `Notes`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.

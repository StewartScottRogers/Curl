---
id: BL-410
title: Send standard error output to the --stderr file in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-410 — Send standard error output to the --stderr file in Curl.Console

## Goal

`--stderr <file>` sends everything curl writes to standard error after the option, `-v` lines and warnings included, to that file; `--stderr -` sends it to standard output.

## Context

- BL-195 parses it into `CommandLineOptions.StandardErrorFile` and leaves opening it to `Curl.Console`. Its Notes, measured on curl 8.21.0: `--stderr se` sends everything written to standard error afterwards, warnings included, to `se`; `--stderr -` sends it to standard output; `--stderr ''` prints `Warning: Warning: Failed to open ` and carries on to standard error.
- BL-242 left it out. Every standard error write in `CurlCommandRunner` goes through its `standardError` stream, and `TransferEventOutput.OpenAsync` takes that stream for `-v`, so one replaced stream carries both.
- Measure first which parser warnings land in the file when `--stderr` comes after the option that raised them.

## Acceptance criteria

- [x] `--stderr se -v` over a scripted handler writes the `-v` lines and a failure's `curl: (N)` line to `se` and nothing to standard error, as measured.
- [x] `--stderr ''` writes the measured doubled warning to standard error and carries on.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel), `file:///nosuchdir/x` and `http://127.0.0.1:1/`:
  - `--stderr se -v` wrote the `-v` lines and `curl: (N)` to `se`, nothing to standard error; `-w "%{stderr}hi\n"` also lands in `se`.
  - `se` is truncated (old content gone) and in text mode (CR LF); `-s --stderr se` still creates it, empty.
  - `--stderr -` sends the lines to standard output.
  - `--stderr ''` and `--stderr adir` (a directory) print `Warning: Warning: Failed to open <name>` (trailing space for `''`), then carry on to standard error.
  - Position matters: `-d @nosuch --stderr se` kept the refusal lines on standard error, `--stderr se -d @nosuch` put them in `se`; `-s --stderr adir` printed nothing, `--stderr adir -s` printed the warning.
  - `--stderr se -O URL` put `Warning: No remote filename` (a transfer-time warning) in `se`.
- Decision (decided by Claude under Stewart's delegation): `CurlCommandRunner` replaces its standard error stream once the parse is accepted, after the parser's warning lines and the config-file note, and closes the file when the run ends. The parser records no option order, so every parser warning stays on standard error, a refusal never reaches the file, and the open-failure warning follows the final `-s`. Why: the runner already sends every standard error write through one stream, so one replacement carries `-v`, warnings, `curl: (N)`, the progress meter and `-w %{stderr}`; making the parser track position is `Curl.Cli` work outside this task's `touches`, filed as BL-476. Recorded here and in `Curl.Console/CLAUDE.md` rather than as an ADR because BL-466, in Doing, touches `Documentation/Planning/Decisions`.
- Tests: `CurlCommandRunnerStandardErrorFileTests` (8).
- Coverage: every new member is at 100%. `Measure-CodeQuality.ps1 -Library Curl.Console` reports 99.48% line / 99.42% branch with two failing members, `DiskWriteOutFileOpener.TryOpen` and `DumpHeaderOutputStream.WriteAsync`; both predate this task and are already filed as BL-432, BL-455 and BL-462. This task adds no failing member.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --stderr <file> now carries -v lines, warnings and curl: (N) lines; --stderr - sends them to standard output

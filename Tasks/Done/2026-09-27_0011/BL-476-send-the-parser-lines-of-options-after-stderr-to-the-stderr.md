---
id: BL-476
title: Send the parser lines of options after --stderr to the --stderr file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-410]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-476 — Send the parser lines of options after --stderr to the --stderr file

## Goal

A parser warning or refusal raised by an option after `--stderr <file>` goes to that file, and one raised before it stays on standard error, as in curl 8.21.0.

## Context

- curl opens the `--stderr` file while it parses the option (`tool_set_stderr_file`), so position decides. Measured on curl 8.21.0 (BL-410 Notes): `-d @nosuch --stderr se` wrote `curl: Failed to open nosuch`, the `option -d` line and the try-help line to standard error; `--stderr se -d @nosuch` wrote all three to `se`. `-s --stderr adir` printed nothing, `--stderr adir -s` printed `Warning: Warning: Failed to open adir`: the `-s` in effect is the one parsed so far.
- BL-410 opens the file in `CurlCommandRunner` after an accepted parse, so every parser warning goes to standard error, a refusal never reaches the file, and the open-failure warning honours the command line's final `-s`.
- `CommandLineParseResult.WarningLines` carries no position; the parser (`Curl.Cli.UnitLibrary`) needs to say which lines came after the `--stderr` option (and whether `-s` was set at that point) so the runner can split them.

## Acceptance criteria

- [x] `--stderr se -H nocolon URL` writes the `-H` warning to `se`; `-H nocolon --stderr se URL` writes it to standard error.
- [x] `--stderr se -d @nosuch URL` writes the measured three refusal lines to `se` and exits 26.
- [x] `--stderr adir -s URL`, with `adir` unopenable, prints the doubled warning; `-s --stderr adir URL` prints nothing.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for `Curl.Cli` or `Curl.Console`.

## Notes

- Filed from BL-410 (2026-09-27).
- Plan: the parser records each `--stderr` as a `StandardErrorRedirect(File, WarningLinesBefore, Silent)` in `CommandLineOptions.StandardErrorRedirects`, surfaced on `CommandLineParseResult.StandardErrorRedirects` for accepted and refused results. `CurlCommandRunner` writes the warning lines up to each redirect, carries it out (open, or warn unless that redirect's `-s`), then writes the rest, the refusal and the run where the last one sends them.
- Measured 2026-09-27 on curl 8.21.0 (Schannel): `--stderr se -H nocolon` puts the warning in `se`, `-H nocolon --stderr se` on standard error; `--stderr a -H nocolon --stderr b` puts the warning in `a` and `curl: (37)` in `b`; `--stderr a -H x --stderr adir` puts the warning, `Warning: Warning: Failed to open adir` and the failure in `a`; `--stderr se -d @nosuch` put the three refusal lines in `se`, exit 26; `--stderr adir -s` warned, `-s --stderr adir` did not.
- Decision (decided by Claude under Stewart's delegation): every `--stderr` is modelled, not only the last, because the measurement shows each one moves the lines after it; a later successful one closes the earlier file. Why: the simplest model that matches every measured case. Recorded here and in `Curl.Console/CLAUDE.md`, not as an ADR, since it refines BL-410's recorded decision rather than making a new one.
- This machine's curl prints `curl: try 'curl --help' for more information`; the repo pins `CommandLineRefusal.TryHelpLine` (with `or 'curl --manual'`), so the test uses that constant and leaves the pinned text alone.
- Tests: `CurlCommandRunnerStandardErrorFileTests` (+7, one replaced), `CommandLineTraceOptionTests` (+3). Measure-CodeQuality: Curl.Cli.UnitLibrary and Curl.Console both 100% line / 100% branch, 0 failing members.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. a --stderr file now receives the parser warnings and refusal lines of the options after it, and its open-failure warning follows the -s read before it

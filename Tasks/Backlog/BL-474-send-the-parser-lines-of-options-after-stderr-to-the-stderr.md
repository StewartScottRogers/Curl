---
id: BL-474
title: Send the parser lines of options after --stderr to the --stderr file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-410]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-474 — Send the parser lines of options after --stderr to the --stderr file

## Goal

A parser warning or refusal raised by an option after `--stderr <file>` goes to that file, and one raised before it stays on standard error, as in curl 8.21.0.

## Context

- curl opens the `--stderr` file while it parses the option (`tool_set_stderr_file`), so position decides. Measured on curl 8.21.0 (BL-410 Notes): `-d @nosuch --stderr se` wrote `curl: Failed to open nosuch`, the `option -d` line and the try-help line to standard error; `--stderr se -d @nosuch` wrote all three to `se`. `-s --stderr adir` printed nothing, `--stderr adir -s` printed `Warning: Warning: Failed to open adir`: the `-s` in effect is the one parsed so far.
- BL-410 opens the file in `CurlCommandRunner` after an accepted parse, so every parser warning goes to standard error, a refusal never reaches the file, and the open-failure warning honours the command line's final `-s`.
- `CommandLineParseResult.WarningLines` carries no position; the parser (`Curl.Cli.UnitLibrary`) needs to say which lines came after the `--stderr` option (and whether `-s` was set at that point) so the runner can split them.

## Acceptance criteria

- [ ] `--stderr se -H nocolon URL` writes the `-H` warning to `se`; `-H nocolon --stderr se URL` writes it to standard error.
- [ ] `--stderr se -d @nosuch URL` writes the measured three refusal lines to `se` and exits 26.
- [ ] `--stderr adir -s URL`, with `adir` unopenable, prints the doubled warning; `-s --stderr adir URL` prints nothing.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member for `Curl.Cli` or `Curl.Console`.

## Notes

- Filed from BL-410 (2026-09-27).

## Log

- 2026-09-27: Created.

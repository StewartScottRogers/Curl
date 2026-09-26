---
id: BL-132
title: Draw curl's -#/--progress-bar bar in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-102, BL-128, BL-131]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-132 — Draw curl's -#/--progress-bar bar in Curl.Console

## Goal

Under `-#`/`--progress-bar`, `Curl.Console` writes curl 8.21.0's progress bar to stderr instead of nothing, byte-identical to curl for the measured cases.

## Context

BL-102 suppresses the meter under `-#` (`CurlCommandRunner.ShowsProgressMeter`) and writes nothing in its place. curl draws a bar of `#` characters with a percentage instead (https://curl.se/docs/manpage.html, `-#, --progress-bar`). The parser already accepts `-#`/`--progress-bar` (BL-119).

**Measure first.** Before writing code, run curl 8.21.0 with `-o` to a file and stderr redirected to a file, and record under Notes the exact stderr bytes (`cat -A` form) and exit code of:

- `curl -# file:///<path>/ten.bin -o out` (ten-byte file),
- `curl -# -C 5 file:///<path>/ten.bin -o out` against a five-byte `out` (does `** Resuming transfer from byte position 5` still appear?),
- `curl -# http://127.0.0.1:<port>/ten.bin -o out` and the same for a 200000-byte file, against `python -m http.server`,
- the 200000-byte case again with `COLUMNS=40` in the environment, to learn how the bar width is chosen.

Then read curl 8.21.0's `src/tool_cb_prg.c` (tag `curl-8_21_0` at https://github.com/curl/curl) and record under Notes how the bar width follows the terminal width and when the bar is redrawn. `CurlCommandRunner` already takes `terminalColumns` (`TerminalColumns`); say under Notes whether it is the right width source.

Live redraws take their byte counts and timing from the console-side sink and clock BL-130 and BL-131 build; a handler that reports none (`file://`) gives the measured `file://` output. Timing reads the injected `TimeProvider`; never `Thread.Sleep`. Every method at cyclomatic complexity 10 or less. Tests use a fake handler and, where timing matters, a hand-rolled `TimeProvider` in `Curl.Console.UnitTests`; no package may be added.

## Acceptance criteria

- [ ] Notes records the command, stderr bytes and exit code of each measured case above, and the bar-width and redraw rules from curl 8.21.0's source with file and function names.
- [ ] A test in `Curl.Console.UnitTests` pins the stderr bytes of `-#` on a ten-byte `file://` transfer (a fake handler reporting no bytes) as measured.
- [ ] A test pins the `-# -C 5` stderr bytes as measured, with or without the resuming line as curl writes it.
- [ ] A test drives a fake handler reporting 200000 of 200000 bytes and pins the final bar as measured, for the default width and for 40 columns.
- [ ] Tests pin that `-s` with `-#` writes no bar, and that `-#` never writes the meter's header lines.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

## Log

- 2026-09-26: Created.

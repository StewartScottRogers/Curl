---
id: BL-132
title: Draw curl's -#/--progress-bar bar in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-102, BL-134, BL-131]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0082-the-progress-bar-replays-curls-callback-from-the-handlers-reports.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
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

- [x] Notes records the command, stderr bytes and exit code of each measured case above, and the bar-width and redraw rules from curl 8.21.0's source with file and function names.
- [x] A test in `Curl.Console.UnitTests` pins the stderr bytes of `-#` on a ten-byte `file://` transfer (a fake handler reporting no bytes) as measured.
- [x] A test pins the `-# -C 5` stderr bytes as measured, with or without the resuming line as curl writes it.
- [x] A test drives a fake handler reporting 200000 of 200000 bytes and pins the final bar as measured, for the default width and for 40 columns.
- [x] Tests pin that `-s` with `-#` writes no bar, and that `-#` never writes the meter's header lines.
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`; new lines and branches are 100% covered.

## Notes

### Measured (curl 8.21.0 x86_64-w64-mingw32 Schannel, Windows, 2026-09-27)

Standard error redirected to a file, `-o out`, files in `Z:/tmp/pb` (`ten.bin` = `0123456789`,
`big.bin` = 200000 random bytes, `zero.bin` empty), HTTP from `python -m http.server 8765`.
`cat -A` form: `^M` is CR, `$` is LF. curl's standard error is in text mode, so each `
` it
writes arrives as CR LF; the runner writes `Environment.NewLine`, as it does for the meter.
`#{72}` means 72 `#` characters.

| Command | Exit | stderr (`cat -A`) |
| --- | --- | --- |
| `curl -# file:///Z:/tmp/pb/ten.bin -o out` | 0 | `^M#{72} 100.0%^M$` |
| `curl -# -C 5 file:///Z:/tmp/pb/ten.bin -o out` (5-byte `out`) | 0 | `^M#{72} 100.0%^M#{72} 100.0%^M$` (no `** Resuming` line; `out` ends `0123456789`) |
| `curl -# -C 10 file:///.../ten.bin -o out` (10-byte `out`) | 0 | `^M#{72} 100.0%^M$` |
| `curl -# -C 5 file:///.../zero.bin -o out` | 36 | `^M#{72} 100.0%curl: (36) failed to resume file:// transfer^M$^M$` |
| `curl -# file:///.../zero.bin -o out` | 0 | `^M$` |
| `curl -# file:///.../missing.bin -o out` | 37 | `curl: (37) Could not open file Z:/tmp/pb/missing.bin^M$` (no newline of the bar's) |
| `curl -# http://127.0.0.1:8765/ten.bin -o out` | 0 | `^M#{72} 100.0%^M$` |
| `curl -# http://127.0.0.1:8765/big.bin -o out` (200000 bytes) | 0 | `^M#{72} 100.0%^M$` |
| `COLUMNS=40 curl -# http://127.0.0.1:8765/big.bin -o out` | 0 | `^M#{33} 100.0%^M$` |
| `curl -# http://127.0.0.1:8765/zero.bin -o out` | 0 | `^M$` |
| `curl -# http://127.0.0.1:8766/x -o out` (refused) | 7 | `curl: (7) Failed to connect ...^M$` only |
| `curl -# -f -w "[%{http_code}]" http://127.0.0.1:8765/nothere -o out` | 22 | `curl: (22) The requested URL returned error: 404^M$^M$` then `[404]` |
| `curl -# http://127.0.0.1:8765/nothere -o out` (no `-f`, 404 page) | 0 | `^M#{72} 100.0%^M$` |
| `curl -# -s http://127.0.0.1:8765/ten.bin -o out` | 0 | empty |
| `COLUMNS=30 curl -# http://127.0.0.1:8767/ -o out`: a script server, HTTP/1.0, no Content-Length, four 1000-byte writes 250 ms apart | 0 | `^M##O=-` + 25 spaces, `^M###O=-` + 24, `^M###=O=-` + 23, `^M## #=O=-` + 22, then `^M$` |

### curl 8.21.0 source (`src/tool_cb_prg.c`, `src/tool_operate.c`, `src/terminal.c`, tag `curl-8_21_0`)

- **Width:** `update_width()` sets the bar width to `get_terminal_columns()` clamped to
  `MIN_BARLENGTH` 20 .. `MAX_BARLENGTH` 400 (20 or fewer columns give 20). It runs in
  `progressbarinit()` and again on every `tool_progress_cb()` call. `get_terminal_columns()`
  (`terminal.c`) reads `COLUMNS` (a number from 21 up to 10000), else on Windows the
  standard-error console's `srWindow.Right - srWindow.Left`, else 79. The bar is
  `"%-Ns %5.1f%%"` with N = width - 7 and `(size_t)(N * point / total)` `#`, so it is
  exactly `width` characters after the CR (79 by default, 40 at `COLUMNS=40`).
- **Redraw (`tool_progress_cb`):** `total = dltotal + ultotal + initial_size` and
  `point = dlnow + ulnow + initial_size`, where `initial_size` is the `-C` offset. The first
  call never returns early. After it, with a known total the call returns without drawing
  when the point has not moved, or when less than 100 ms have passed since the last call
  that went on and the point is below the total. With an unknown total (0) it returns when
  less than 100 ms have passed, else draws `fly()`: the `-=O=-` block, which moves one
  column per call when the point moved and bounces at 0 and at width - 6, crossed by four
  `#` placed by a 200-entry sine table from `tick` (which starts at 150 and steps 2). Then it
  counts the call (`bar->calls++`) and draws the bar when the total is above 0 and the point
  moved (a point past the total counts as 100%).
- **Newline:** `post_per_transfer()` (`tool_operate.c`) writes `
` to the bar's stream when
  `progressmode == CURL_PROGRESS_BAR && per->progressbar.calls`. By then the failure line is
  already out, and the `-w` output comes after.
- **When:** the bar callback is used only while `noprogress` is off, so `-s`,
  `--no-progress-meter`, and a body on a terminal (`isatty` on the output stream) turn it
  off, as they do the meter.
- **Width source:** `CurlCommandRunner`'s `terminalColumns` (`TerminalColumns.Resolve`)
  follows the same `COLUMNS` / console / 79 rule, so it is the right source. It is resolved
  once per run, whereas curl reads it on every call. See ADR-0082.

### Decisions (ADR-0082, decided by Claude under Stewart's delegation)

- **Calls:** `ReportTransferStarted` is curl's first callback call, with no bytes, and each
  byte report is a further call. This explains the measured cases. `-C 5` on `file://`
  draws twice: the first call has only the offset (5 of 5), and the last call has 10 of 10.
  An empty file draws nothing but the newline. `-f` 404 ran the callback without drawing,
  so it writes only the newline. A refused connect never ran it, so it writes nothing.
- **A handler that reports no bytes** (`file://`) gets one last call after a success, with
  `BytesTransferred` as both the bytes and the total. The `file://` handler is not changed,
  so its meter still stays at the zero line.
- **Timing** reads the runner's `TimeProvider`. Tests use `ManualTimeProvider`.
- **touches widened** (rule 3) to `Documentation/Planning/Decisions/ADR-0082-...md` and the
  Decisions `README.md` index, for the ADR. No task in Doing names either file (the others
  touch Curl.Core and Curl.Protocol.Http).

### Delivered

- `Curl.Console/ProgressBarRecorder.cs` (new). `TransferProgressRecorder` passes each report
  on to it. `CurlCommandRunner.StartTransferProgress` creates it under `-#`, and
  `WriteProgressAsync` (renamed from `WriteProgressMeterAsync`) writes the bar.
  `TransferAndReportAsync` writes the newline after the failure lines.
- Tests: `CurlCommandRunnerProgressBarTests` (16), `ProgressBarRecorderTests` (24),
  `TransferProgressRecorderTests` (+1). The old `-#` test in
  `CurlCommandRunnerProgressMeterTests` now checks that no meter or resuming lines appear.
  Curl.Console.UnitTests: 782 passed. `ProgressBarRecorder` and `TransferProgressRecorder`
  are at 100% line and branch coverage, and every changed `CurlCommandRunner` method is at
  100%. `UrlOutputOf` was already at 50% branch before this task and was not touched.
- `dotnet format` flags end-of-line markers in `Curl.Console/CurlComposition.cs`. That file
  was not touched and the issue was already there. Every file this task changed is
  format-clean.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -#/--progress-bar draws curl 8.21.0's bar (full bar, -C resume, 40 columns, fly animation) and its newline, byte-identical to the measured cases

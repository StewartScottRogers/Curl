---
id: BL-092
title: Wrap warning lines at curl's terminal width
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-087]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-092 — Wrap warning lines at curl's terminal width

## Goal

Every `Warning: ` line `Curl.Console` writes to standard error is wrapped exactly as curl
8.21.0 wraps it, at the width curl derives from `COLUMNS` or the console, so long warnings
produce the same stderr bytes as upstream.

## Context

Measured 2026-09-26 against the local curl 8.21.0 (x86_64-w64-mingw32). curl's `warnf`
(`src/tool_msgs.c`, `voutf`) prints `Warning: ` and then the text. With
`width = columns - 9` (9 is the length of `Warning: `): while the remaining text is longer
than `width`, it cuts at the last blank at or before index `width - 1` of the remaining
text (or at `width - 1` when there is no blank), writes the text up to and including that
blank, a newline, then starts the rest on a new line with a fresh `Warning: ` prefix. The
last piece is written followed by a newline. A cut blank stays at the end of the line
(trailing space).

`columns` is resolved in this order:
1. The `COLUMNS` environment variable, used only when it parses as a number from 21 to
   9999 inclusive.
2. Otherwise the console width of standard error (window `Right - Left` on Windows; the
   BCL exposes this as `Console.WindowWidth` when stderr is a console).
3. Otherwise 79.

Measured cases, all with `curl --no-progress-meter -o C:/Windows/System32/bl087.txt file:///C:/Windows/win.ini`
(the warning text is `Failed to open the file C:/Windows/System32/bl087.txt: Permission denied`):
- Default 80-column console (columns 79 → width 70): two lines,
  `Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission ` (trailing
  space) and `Warning: denied`.
- `COLUMNS=200`: one line,
  `Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission denied`.
- `COLUMNS=40` (width 31): three lines, `Warning: Failed to open the file ` (trailing space),
  `Warning: C:/Windows/System32/bl087.txt: ` (trailing space), `Warning: Permission denied`.

Where it lands. Today both warning producers return one unwrapped line:
`Curl.Console/OutputFileOpenWarning.cs` (from BL-087, used by
`Curl.Console/DeferredOutputFileStream.cs`) and
`Curl.Cli.UnitLibrary/CommandLineWarning.cs` (`FileNameLooksLikeFlag`), whose lines are
written by `Curl.Console/CurlCommandRunner.cs`. Put the wrapping in `Curl.Console`, at the
point that writes warning lines to stderr, so both producers are wrapped without changing
`Curl.Cli.UnitLibrary`: a `Curl.Console` type (for example `WarningLineWrapper`) that takes
the text after `Warning: ` and a column count and returns the wrapped lines, plus a
separate resolver for the column count that reads `COLUMNS` and the console width through
injected seams (an environment-variable reader and a console-width source), so tests never
touch the real environment or console. Keep each method at cyclomatic complexity 10 or
less; `Curl.Console` is held to 100% line and branch coverage.

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` named for the three measured cases pass, each with
      an injected column count (79, 200, 40) and each asserting the exact lines above,
      trailing spaces included.
- [x] A test covers a text with no blank before `width - 1`, asserting the cut at
      `width - 1`.
- [x] Column-resolution tests pass for: `COLUMNS=200` used; `COLUMNS=21` and `COLUMNS=9999`
      used; `COLUMNS=20`, `COLUMNS=10000` and a non-numeric `COLUMNS` ignored in favour of
      the console width; no `COLUMNS` and no console width gives 79.
- [x] A `CurlCommandRunner` test shows the BL-087 open-failure warning written to stderr
      wrapped at an injected width of 79, matching the two-line measured output byte for
      byte.
- [x] `dotnet build Curl.Console -warnaserror` and
      `dotnet build Curl.Console.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test needs
      `TestCategory=Integration`.

## Notes

- Plan: `WarningLineWrapper` (`WrapText` for the text after `Warning: `, `WrapLine` for a
  whole stderr line, passing non-warning lines through) and `TerminalColumns` (`Resolve`
  over injected `COLUMNS` value and console-width reader). `CurlCommandRunner` takes a
  `terminalColumns` constructor parameter (default 79) and wraps inside
  `WriteErrorLineAsync`, so parser warnings, the BL-087 open-failure warning and the
  after-transfer warning are all wrapped; `CurlComposition.CreateRunner` passes
  `TerminalColumns.Resolve()`. The test-only connector overload keeps 79 so its tests do
  not depend on the host console.
- Choice: the console width is one less than `Console.WindowWidth` on Windows, because
  curl uses the window's `Right - Left` there (80-column console -> 79, as measured); on
  other platforms curl uses `ws_col` unchanged, so the window width is used as-is.
- Choice: `COLUMNS` is parsed like curl's `strtol` check - leading white space and a sign
  allowed, nothing after the digits - with `NumberStyles.AllowLeadingWhite |
  AllowLeadingSign`, invariant culture.
- Choice: a console width of 0 or 10000+ falls back to 79 (curl's `cols >= 0 && cols <
  10000`, then `!width -> 79`). A width no wider than the prefix (columns <= 9, only
  reachable from a tiny console) leaves the text whole instead of reproducing curl's
  `size_t` underflow.
- curl's cut rule also treats a blank at index 0 as "no blank" and cuts at `width - 1`;
  replicated and tested. Tabs count as blanks (`ISBLANK`).
- Found: the two `-r` range warnings in `Curl.Cli.UnitLibrary` are pre-cut at 79, so at
  other widths they still differ from curl. Outside this task's `touches`; filed as
  BL-113.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Console` - 100% line, 100% branch,
  worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Warning lines on stderr wrap at curl's terminal width (COLUMNS, stderr console, else 79), byte for byte with curl 8.21.0

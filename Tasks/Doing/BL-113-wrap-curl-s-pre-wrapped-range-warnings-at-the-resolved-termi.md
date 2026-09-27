---
id: BL-113
title: Wrap curl's pre-wrapped range warnings at the resolved terminal width
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-092]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-113 — Wrap curl's pre-wrapped range warnings at the resolved terminal width

## Goal

The two `-r` range warnings (`CommandLineWarning.RangeHasNoDash` and
`CommandLineWarning.RangeHasInvalidCharacter`) come out wrapped exactly as curl 8.21.0 wraps
them at any terminal width, not only at 79 columns.

## Context

`Curl.Cli.UnitLibrary/CommandLineWarning.cs` returns these two warnings already cut into
lines at curl's default 79 columns. Since BL-092, `Curl.Console` wraps every `Warning: `
line at the width `TerminalColumns` resolves, one line at a time (`WarningLineWrapper`).
At 79 columns the result is right, because each pre-cut piece fits. At any other width it
is wrong: with `COLUMNS=200` curl prints each as one line and we print two or three, and at
narrow widths re-wrapping each piece differs from wrapping the whole text. Fix: have
`CommandLineWarning` return each range warning as one unwrapped `Warning: ` line (as
`FileNameLooksLikeFlag` already does), and let the console wrap it. Production code
changes only in `Curl.Cli.UnitLibrary`; `Curl.Console` needs no change, but three tests in
`Curl.Console.UnitTests` build their expected stderr from these properties and must be
updated (see Notes).

## Acceptance criteria

- [ ] `CommandLineWarning.RangeHasNoDash` and `RangeHasInvalidCharacter` are each one
      unwrapped line: `Warning: A specified range MUST include at least one dash (-). Appending one for you`
      and `Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.`
- [ ] `Curl.Cli.UnitTests` tests pin both texts, and every parser test that expected the
      pre-cut lines is updated.
- [ ] In `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs`, the three tests
      `RunAsync_RangeWithInvalidCharacter_PrintsTheWarningThenExit33`,
      `RunAsync_RangeWithNoDash_PrintsTheWarningBeforeAnyTransferOutput` and
      `RunAsync_RefusedCommandLine_PrintsTheWarningBeforeTheRefusal` expect the runner's
      default 79-column wrap as literal lines - the pieces `CommandLineWarning` returns today,
      `Warning: A specified range MUST include at least one dash (-). Appending one ` /
      `Warning: for you` and
      `Warning: Invalid character is found in given range. A specified range MUST ` /
      `Warning: have only digits in 'start'-'stop'. The server's response to this ` /
      `Warning: request is uncertain.` (each trailing space kept) - and pass. The `Lines`
      helper is changed or replaced so it compiles against the new property type. No file
      under `Curl.Console` changes.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-09-26 (lane 3): Blocked before any code change. `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs`
  (lines 66, 94, 107, helper `Lines` at 258) builds its expected stderr from
  `CommandLineWarning.RangeHasNoDash` / `RangeHasInvalidCharacter` joined unwrapped, while the
  runner under test wraps at the default 79 columns. Once each warning is one unwrapped line,
  those three tests expect the unwrapped text but get the 79-column wrap, so they fail (and a
  change of type from list to string would not compile there). The fix needs
  `Curl.Console.UnitTests` in `touches`: pin the literal 79-column pieces there (or wrap the
  expectation), then this task is the one-line change the Goal describes.
- Re-planned 2026-09-26: `touches` now includes `Curl.Console.UnitTests`, and a criterion
  names the three console tests and the literal 79-column lines they must expect.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Needs Curl.Console.UnitTests added to touches: three CurlCommandRunnerTransferOptionTests build expected stderr from the unwrapped range warnings and fail at 79 columns; re-plan to include it
- 2026-09-26: Blocked -> Backlog. Re-planned: touches adds Curl.Console.UnitTests; a criterion names the three CurlCommandRunnerTransferOptionTests and the literal 79-column lines they must expect.
- 2026-09-27: Backlog -> Doing.

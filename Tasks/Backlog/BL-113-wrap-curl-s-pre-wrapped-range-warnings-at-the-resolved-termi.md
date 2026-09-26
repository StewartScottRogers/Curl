---
id: BL-113
title: Wrap curl's pre-wrapped range warnings at the resolved terminal width
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-092]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
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
`FileNameLooksLikeFlag` already does), and let the console wrap it. This touches only
`Curl.Cli.UnitLibrary` and its tests; `Curl.Console` needs no change.

## Acceptance criteria

- [ ] `CommandLineWarning.RangeHasNoDash` and `RangeHasInvalidCharacter` are each one
      unwrapped line: `Warning: A specified range MUST include at least one dash (-). Appending one for you`
      and `Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.`
- [ ] `Curl.Cli.UnitTests` tests pin both texts, and every parser test that expected the
      pre-cut lines is updated.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.

---
id: BL-389
title: Wrap the -z date and request-method warnings at the resolved terminal width
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-389 — Wrap the -z date and request-method warnings at the resolved terminal width

## Goal

`CommandLineWarning.TimeConditionIsNotADate`, `PostRequestedWithHead`, `PostRequestedWithGet` and
the `OnlyOneRequestMethod` warnings come out wrapped exactly as curl 8.21.0 wraps them at any
terminal width, not only at 79 columns.

## Context

Same defect BL-113 fixed for the two `-r` range warnings. `Curl.Cli.UnitLibrary/CommandLineWarning.cs`
returns these warnings already cut at 79 columns (`TimeConditionIsNotADate` as a literal two-line
list, the request-method ones through `WrappedMessage.Lines`). `Curl.Console` re-wraps each
`Warning: ` line at the width `TerminalColumns` resolves (`WarningLineWrapper`), so at 79 columns the
output is right, but with `COLUMNS=200` curl prints one line and we print two, and at narrow widths
re-wrapping each piece differs from wrapping the whole text. Fix as BL-113 did: return each as one
unwrapped `Warning: ` line and let the console wrap it. Tests in `Curl.Cli.UnitTests` and
`Curl.Console.UnitTests` that expect the pre-cut lines must change; measure real curl at
`COLUMNS=200` and `COLUMNS=40` before pinning (e.g. `curl -z notadate -o NUL file:///<file>`,
`curl -I -d x http://127.0.0.1:1/`, `curl -F a=b -I --bogus x`). Check whether `WrappedMessage` has
other callers before removing it.

## Acceptance criteria

- [x] Each of `TimeConditionIsNotADate`, `PostRequestedWithHead`, `PostRequestedWithGet` and
      `OnlyOneRequestMethod(...)` yields one unwrapped `Warning: ` line.
- [x] `Curl.Cli.UnitTests` pins the unwrapped texts; every parser test that expected pre-cut lines is updated.
- [x] `Curl.Console.UnitTests` has, for the `-z` warning and one request-method warning, a runner test at
      200 columns (one line) and at 40 columns (curl's measured wrap), and the existing 79-column
      expectations still pass. No file under `Curl.Console` changes.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Filed 2026-09-27 from BL-113 (lane 2), which fixed only the range warnings.
- Delivered directly rather than through the full `/feature` stages: the change is BL-113's pattern
  applied to four more warnings (return the text unwrapped, let `WarningLineWrapper` wrap it), with no
  design question and no new seam.
- Measured 2026-09-27 against curl 8.21.0 (Schannel, Windows) at `COLUMNS=200` and `COLUMNS=40`:
  `curl -z notadate -o NUL file:///<global.json>`, `curl -I -d x http://127.0.0.1:1/`,
  `curl --no-head -d x http://127.0.0.1:1/`, `curl -F a=b -I --bogus x`. At 200 each warning is one
  line; at 40 curl wraps the whole text, which is what `WarningLineWrapper` produces from the unwrapped line.
- `WrappedMessage` stays: `ConfigFileSyntax`, `VariableDefinition`, `VariableExpansion`,
  `FormPartParameterReader`, `MultipartFormField`, `CommandLineRefusal`, `CommandLineOptions` and
  `UnrecognizedFtpFileMethod` still call it. Those pre-cut texts have the same defect at other widths;
  not widened into this task.
- `dotnet format --verify-no-changes` reports line-ending errors in `Curl.Console.UnitTests/DumpHeaderOutputStreamTests.cs`,
  a file this task does not touch (from BL-388); the files this task changed pass.
- Tests: Curl.Cli.UnitTests 2012 passed (13 skipped), Curl.Console.UnitTests 900 passed; whole fast run green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The -z date and request-method warnings wrap at any terminal width as curl 8.21.0 does

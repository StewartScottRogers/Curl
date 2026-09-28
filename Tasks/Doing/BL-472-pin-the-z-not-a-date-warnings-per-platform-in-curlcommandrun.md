---
id: BL-472
title: Pin the -z not-a-date warnings per platform in CurlCommandRunnerTransferOptionTests
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-472 — Pin the -z not-a-date warnings per platform in CurlCommandRunnerTransferOptionTests

## Goal

The three `-z` not-a-date tests in `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs` pin the Windows (Schannel build) warning text on Windows only, and three non-Windows twins pin the text curl's non-Windows build writes, so the file passes on Windows, Linux and macOS.

## Context

Follow-up of BL-427 (get a fully green CI run on all three OSes). CI run 36376508151 on `work/dark-factory` (head 46db192) failed only on ubuntu-latest and macos-latest; windows-latest passed.

Failing tests (`Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs`, about lines 367-405):

- `RunAsync_TimeCondNotADate_WarnsAndTransfersUnconditionally`
- `RunAsync_TimeCondNotADateAt200Columns_WarnsOnOneLine`
- `RunAsync_TimeCondNotADateAt40Columns_WrapsTheWarningAsCurlWrapsIt`

Off Windows, production writes an extra first warning, `Warning: Failed to get filetime: No such file or directory`, before the "Illegal date format" lines, as curl's non-Windows build does when it tries the `-z` value as a file name (`getfiletime` in curl's `src/tool_filetime.c`, called from the `-z` handling in `src/tool_getparam.c`). The tests pin only the Windows text. BL-424 fixed the same kind of platform assumption in `Curl.Cli.UnitTests`; follow its pattern.

Actual stderr on ubuntu-latest and macos-latest, from the CI run:

- Default width:
  `"Warning: Failed to get filetime: No such file or directory\nWarning: Illegal date format for -z, --time-cond (and not a filename). \nWarning: Disabling time condition. See curl_getdate(3) for valid date syntax.\n"`
- 200 columns:
  `"Warning: Failed to get filetime: No such file or directory\nWarning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.\n"`
- 40 columns:
  `"Warning: Failed to get filetime: No \nWarning: such file or directory\nWarning: Illegal date format for -z, \nWarning: --time-cond (and not a \nWarning: filename). Disabling time \nWarning: condition. See curl_getdate(3) \nWarning: for valid date syntax.\n"`

Fix in the test file only: mark the three existing tests `[OSCondition(OperatingSystems.Windows)]` (renaming them if needed so the name says Windows) and add three twins marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` that pin the texts above. Do not change production code.

## Acceptance criteria

- [ ] The three existing tests carry `[OSCondition(OperatingSystems.Windows)]` and still pin the Windows text.
- [ ] Three new tests carry `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` and assert the exact default-width, 200-column and 40-column stderr strings listed in Context.
- [ ] The Notes section records the source of the non-Windows text: curl's `src/tool_filetime.c` / `src/tool_getparam.c` (with the curl version or commit checked) or a Linux measurement with `Record-CurlExchange.ps1`.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [ ] The next CI run shows 0 failures in `Curl.Console.UnitTests` on ubuntu-latest and macos-latest.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.

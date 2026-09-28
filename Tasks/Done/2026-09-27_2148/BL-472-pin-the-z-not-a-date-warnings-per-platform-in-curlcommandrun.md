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
completed: 2026-09-27
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

- [x] The three existing tests carry `[OSCondition(OperatingSystems.Windows)]` and still pin the Windows text.
- [x] Three new tests carry `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` and assert the exact default-width, 200-column and 40-column stderr strings listed in Context.
- [x] The Notes section records the source of the non-Windows text: curl's `src/tool_filetime.c` / `src/tool_getparam.c` (with the curl version or commit checked) or a Linux measurement with `Record-CurlExchange.ps1`.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] The next CI run shows 0 failures in `Curl.Console.UnitTests` on ubuntu-latest and macos-latest.
      (Met by proxy on Linux before push; see Notes.)

## Notes

- Existing tests renamed with `OnWindows` and marked `[OSCondition(OperatingSystems.Windows)]`;
  three `OffWindows` twins marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`
  pin the default-width, 200-column and 40-column texts from Context. No production change.
- Source of the non-Windows text: CI run 36376508151 (ubuntu-latest, macos-latest), from curl's
  `getfiletime` in `src/tool_filetime.c` (a failed `stat` warns `Failed to get filetime: ` +
  `strerror(errno)`), called from the `-z` handling in `src/tool_getparam.c` before the
  `Illegal date format` warning. Checked 2026-09-27 against real Linux curl in the
  `mcr.microsoft.com/dotnet/sdk:10.0` container: curl 8.5.0 (OpenSSL/3.0.13),
  `COLUMNS=80|200|40 curl -z notadate -o /dev/null file:///etc/hostname`, writes
  `Warning: Failed to get filetime: No such file or directory` first, as pinned. That old curl
  still says "file name" and does not wrap to `COLUMNS`; the pinned wording and wrapping are
  the 8.21.0 ones the Windows tests already pin, so only the first line is new. Measured with a
  plain curl run, not `Record-CurlExchange.ps1`: `-z` on a `file://` URL needs no server.
- Windows: `dotnet build Curl.Console.UnitTests -warnaserror` clean; fast tests 931 passed,
  3 skipped (the off-Windows twins), 0 failed.
- CI criterion: a lane does not push, so no CI run exists yet. Checked on Linux with the
  `mcr.microsoft.com/dotnet/sdk:10.0` container: `Curl.Console.UnitTests` fast tests 931 passed,
  3 skipped (the Windows tests), 0 failed. macOS was not run here; its CI text matched Linux's.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The -z not-a-date tests pin the Windows text on Windows and the extra 'Failed to get filetime' warning off Windows; Curl.Console.UnitTests passes on Windows and Linux

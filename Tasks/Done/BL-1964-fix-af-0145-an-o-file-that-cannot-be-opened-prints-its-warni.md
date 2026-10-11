---
id: BL-1964
title: Fix AF-0145: An -o file that cannot be opened prints its Warning line inside the progress-meter row instead of after the meter's closing newline
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1964 — Fix AF-0145: An -o file that cannot be opened prints its Warning line inside the progress-meter row instead of after the meter's closing newline

## Goal

The defect the audit office reported as AF-0145 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0145 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0145-an-o-file-that-cannot-be-opened-prints-its-warning.md`.

Location: `Curl.Console/CurlCommandRunner.cs:4029`

Location: `Curl.Console/CurlCommandRunner.cs:4029`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Invoke-DifferentialConformance.ps1 -Count 300 -Seed 455559493, case 233 (--output-dir missing.txt --output f.txt URL) differed in stderr only; both exit 23 with the same request. Reduced to --output missing.txt/f.txt URL (reproduced 3 of 3 runs). curl's stderr ends '... 0\r\nWarning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n': the meter row is closed with its newline before the warning. Curl's ends '... 0Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n\r\n': the warning is written mid-row, and the meter's end newline (WriteProgressAsync, CurlCommandRunner.cs:4029) comes after it as a stray blank line. With -s both stderr outputs are empty (identical). Phase 2: no ADR under Documentation/Planning/Decisions records this ordering as deliberate.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\ac-odir"; $a=@('--output','missing.txt/f.txt','http://127.0.0.1:50994/'); & ./Record-CurlExchange.ps1 -Port 50994 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50994 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl: ' + @(Select-String -Path "$o\curl\stderr.txt" -Pattern '^Warning: Failed').Count + ' / Curl: ' + @(Select-String -Path "$o\candidate\stderr.txt" -Pattern '^Warning: Failed').Count
```

- Expected: curl: 1 / Curl: 1 (the Warning line starts its own line after the meter's newline, no trailing blank line)
- Actual: curl: 1 / Curl: 0 (Curl's warning follows the meter row on the same line: '...0Warning: Failed to open the file missing.txt/f.txt: No such file or directory', then an extra CRLF)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: `TransferIntoOutputFileAsync` printed the `-o` open warning as soon as the transfer returned, before `WriteProgressAsync` wrote the meter's closing newline, so the warning landed on the live meter row and the newline became a stray blank line.
- Fix: the warning is held as `RunningTransferState.OutputFileOpenWarning` and printed by `WriteProgressAndAbandonedRetryWarningAsync` right after the meter (before any abandoned-retry warning). Under `-#` it is still printed at once: the bar writes its newline after the failure lines, and no measurement says otherwise. The retried-attempt path (`SettleRetriedOutputFileAsync`, upstream test 3036) was left as it is, since its order is pinned by that measured test.
- `touches` widened to `Curl.Console.UnitTests` for the regression test; no task in Doing on origin/work/dark-factory named it (only BL-2038, Curl.Conformance.*).
- Test: `CurlCommandRunnerStartedTransferProgressMeterTests.RunAsync_StartedEmptyTransferToUnopenableOutputFile_WritesTheOpenWarningAfterTheMetersNewline`.
- Reproduction rerun 2026-10-10 on a Release build: `curl: 1 / Curl: 1`. Remaining difference: curl draws three more zero rows (its success done rows) before the newline; filed as BL-2039.
- Under `-#` the immediate print is pinned by `CurlCommandRunnerProgressBarTests.RunAsync_ProgressBarWithAnUnopenableOutputFile_WritesTheOpenWarningBeforeTheBar` (covers the helper's other branch).
- Fast tests: Curl.Console.UnitTests 2783 passed, 25 skipped; whole solution fast run had no failures.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. An -o file that cannot be opened prints its Warning line after the progress meter's closing newline, as curl 8.21.0 does

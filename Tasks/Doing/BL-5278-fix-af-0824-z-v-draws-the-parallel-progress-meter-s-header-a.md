---
id: BL-5278
title: Fix AF-0824: -Z -v draws the parallel progress meter's header and first line only after the response head, where curl draws them right after 'Trying'
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ParallelProgressMeter.cs, Curl.Engine.UnitLibrary/ParallelTransferProgress.cs, Curl.Console/ConnectionIdRecordingTransferEvents.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/ConnectionIdRecordingTransferEventsTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5278 — Fix AF-0824: -Z -v draws the parallel progress meter's header and first line only after the response head, where curl draws them right after 'Trying'

## Goal

The defect the audit office reported as AF-0824 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0824 (Low, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:96`

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:96`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 213433, case 247 (--parallel -v --quote GET --ntlm). Reduced to `--parallel -v http://127.0.0.1:PORT/` and seen the same on two reruns. curl's stderr order: `*   Trying 127.0.0.1:PORT...`, `DL% UL%  Dled ...` header, `\r--  --      0     0     1     1 ... 0      ` (no newline), then `* Established connection`, the request and response lines, `* Connection #0 ... left intact`, and the final line. Curl's order: Trying, Established, request, response, then the header, a status line, `* Connection #0 ... left intact`, and the final line. Exit codes match (0). Byte counts and the final line agree, so only where the first draw lands differs. Cause not pinned down: AddTransfer (line 96) never draws, and the first draw in this run comes only after the response head. curl's first draw follows its first curl_multi_perform, during the connect. Phase 2: ADR-0155 lists the events a draw follows, including 'the runner having started every transfer it can'. That should draw before the connect, as curl does, but the measured run does not. The ADR does not record this ordering as deliberate.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 62211 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\par-curl -CurlArgs @('--parallel','-v','http://127.0.0.1:62211/') *> $null; ./Record-CurlExchange.ps1 -Port 62211 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\par-cand -CurlArgs @('--parallel','-v','http://127.0.0.1:62211/') *> $null; (Get-Content $env:TEMP\par-curl\stderr.txt | Select-String -Pattern '^DL%','^> GET').LineNumber; (Get-Content $env:TEMP\par-cand\stderr.txt | Select-String -Pattern '^DL%','^> GET').LineNumber
```

- Expected: The `DL%` header line comes before the `> GET` line (curl: header before the request)
- Actual: In Curl's stderr the `DL%` header line comes after `> GET` and the response head

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix: a `-Z` transfer draws the meter when its connect attempt reports `  Trying ...` (`ConnectionIdRecordingTransferEvents` -> `ParallelTransferProgress.ReportConnectAttemptStarted`), as curl does after its first multi_perform. The recorder wraps events for any parallel transfer. Repro now gives Trying, DL% header, then > GET. Widened touches (no other Doing task holds these files). Engine quality report: 0 failing. Platform-neutral.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

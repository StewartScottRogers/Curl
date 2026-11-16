---
id: AF-0824
title: -Z -v draws the parallel progress meter's header and first line only after the response head, where curl draws them right after 'Trying'
auditor: conformance
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: conformance:Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:--parallel:stderr
reproduction: none
task: BL-5278
tasks: BL-5278
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0824 - -Z -v draws the parallel progress meter's header and first line only after the response head, where curl draws them right after 'Trying'

## Summary

Low finding from the conformance auditor at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:96`: -Z -v draws the parallel progress meter's header and first line only after the response head, where curl draws them right after 'Trying'.

## Evidence

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:96`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 213433, case 247 (--parallel -v --quote GET --ntlm). Reduced to `--parallel -v http://127.0.0.1:PORT/` and seen the same on two reruns. curl's stderr order: `*   Trying 127.0.0.1:PORT...`, `DL% UL%  Dled ...` header, `\r--  --      0     0     1     1 ... 0      ` (no newline), then `* Established connection`, the request and response lines, `* Connection #0 ... left intact`, and the final line. Curl's order: Trying, Established, request, response, then the header, a status line, `* Connection #0 ... left intact`, and the final line. Exit codes match (0). Byte counts and the final line agree, so only where the first draw lands differs. Cause not pinned down: AddTransfer (line 96) never draws, and the first draw in this run comes only after the response head. curl's first draw follows its first curl_multi_perform, during the connect. Phase 2: ADR-0155 lists the events a draw follows, including 'the runner having started every transfer it can'. That should draw before the connect, as curl does, but the measured run does not. The ADR does not record this ordering as deliberate.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 62211 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\par-curl -CurlArgs @('--parallel','-v','http://127.0.0.1:62211/') *> $null; ./Record-CurlExchange.ps1 -Port 62211 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\par-cand -CurlArgs @('--parallel','-v','http://127.0.0.1:62211/') *> $null; (Get-Content $env:TEMP\par-curl\stderr.txt | Select-String -Pattern '^DL%','^> GET').LineNumber; (Get-Content $env:TEMP\par-cand\stderr.txt | Select-String -Pattern '^DL%','^> GET').LineNumber
```

- Expected: The `DL%` header line comes before the `> GET` line (curl: header before the request)
- Actual: In Curl's stderr the `DL%` header line comes after `> GET` and the response head

## Re-audits

## Log

- 2026-11-16: filed proposed.

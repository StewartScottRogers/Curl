---
id: AF-0237
title: --parallel with a slow --interface bind failure: Curl prints meter rows before the curl: (45) line and runs the error on after them; curl prints the error first
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Curl.Console/ParallelProgressMeter.cs:--parallel:stderr
reproduction: none
task: BL-2574
tasks: BL-2574
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0237 - --parallel with a slow --interface bind failure: Curl prints meter rows before the curl: (45) line and runs the error on after them; curl prints the error first

## Summary

Low finding from the conformance auditor at `Curl.Console/ParallelProgressMeter.cs`: --parallel with a slow --interface bind failure: Curl prints meter rows before the curl: (45) line and runs the error on after them; curl prints the error first.

## Evidence

Location: `Curl.Console/ParallelProgressMeter.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 374343970, case 189 (--parallel --interface - --crlf URL), reduced to --parallel --interface - URL; reproduced twice. curl stderr starts 'curl: (45) Failed to connect to 127.0.0.1:PORT after N ms: Failed binding local connection end' as its own first line, then the meter header and rows. Curl stderr starts with the meter header, then '\r--  --      0     0     1     1 ...' rows drawn during the ~2.7 s bind attempt, with 'curl: (45) ...' run on after the last row on the same line. Both exit 45, and without --parallel both print just the error line. A probable cause, not confirmed: curl's interface lookup blocks its parallel loop, so it draws nothing until the transfer fails, while Curl's asynchronous connect lets ParallelProgressMeter draw on its one-second tick (ADR-0155). Phase 2: ADR-0155 describes drawing on the loop's events and its one-second poll; it does not cover a connect that blocks curl's loop.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; foreach ($c in @('C:\Program Files\Git\ucrt64\bin\curl.exe', (Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path)) { $o = Join-Path $env:TEMP ('t189-' + [guid]::NewGuid()); .\Record-CurlExchange.ps1 -Port 63411 -Curl $c -OutDirectory $o -CurlArgs @('--parallel','--interface','-','http://127.0.0.1:63411/') *> $null; '--'; (Get-Content (Join-Path $o 'stderr.txt'))[0] }
```

- Expected: Each binary's first stderr line is 'curl: (45) Failed to connect to 127.0.0.1:63411 after N ms: Failed binding local connection end'
- Actual: curl's first line is the curl: (45) line; Curl's first line is the meter header 'DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed', and its curl: (45) text follows meter rows on line 2

## Re-audits

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.

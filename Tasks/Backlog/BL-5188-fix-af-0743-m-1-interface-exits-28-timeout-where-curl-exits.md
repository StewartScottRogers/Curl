---
id: BL-5188
title: Fix AF-0743: -m 1 --interface - exits 28 (timeout) where curl exits 45 (bind failed)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-11-14
completed:
---
# BL-5188 — Fix AF-0743: -m 1 --interface - exits 28 (timeout) where curl exits 45 (bind failed)

## Goal

The defect the audit office reported as AF-0743 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0743 (High, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console/CurlCommandRunner.cs`

Location: `Curl.Console/CurlCommandRunner.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Seed 641133, case 114 (-m 1 --interface - --referer '' --pubkey - URL), reduced to '-m 1 --interface - URL'. curl: exit 45, 'curl: (45) Failed to connect to 127.0.0.1:62811 after 2967 ms: Failed binding local connection end'. Curl: exit 28, 'curl: (28) Connection timed out after 1012 milliseconds'. Without -m both exit 45 (curl after 2955 ms, Curl after 2697 ms). curl's bind lookup of the interface name blocks and does not honour -m; Curl's MaxTimeWatchdog cancels the bind and reports 28. The difference depends on how long resolving the interface name takes on the machine. I did not trace the cause in Curl's code. Phase 2: ADR-0269 covers --interface binding per dial and does not record a divergence from curl under -m.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; & ./Record-CurlExchange.ps1 -Port 62811 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\i-curl -CurlArgs @('-m','1','--interface','-','http://127.0.0.1:62811/'); & ./Record-CurlExchange.ps1 -Port 62811 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\i-cand -CurlArgs @('-m','1','--interface','-','http://127.0.0.1:62811/'); Get-Content $env:TEMP\i-curl\exitcode.txt, $env:TEMP\i-cand\exitcode.txt
```

- Expected: 45 and 45, 'curl: (45) Failed to connect to 127.0.0.1:62811 after N ms: Failed binding local connection end'
- Actual: 45 (curl) and 28 (Curl), 'curl: (28) Connection timed out after 1012 milliseconds'

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.

---
id: BL-5371
title: Fix AF-0851: --mptcp -Z on Windows: Curl shows the transfer live in the parallel meter before the exit 7 error, where curl fails first and the meter shows Live 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpDialer.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5371 — Fix AF-0851: --mptcp -Z on Windows: Curl shows the transfer live in the parallel meter before the exit 7 error, where curl fails first and the meter shows Live 0

## Goal

The defect the audit office reported as AF-0851 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0851 (Low, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:382`

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:382`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 320622, case 237 (--mptcp -Z), reproduced 3 out of 3 times. Both exit 7 and send no request. curl's stderr: 'curl: (7) Failed to connect to 127.0.0.1:PORT after 0 ms: Could not connect to server', then the parallel meter header, then rows '--  --      0     0     1     0 ...'. Curl's stderr: the meter header first, then a row '--  --      0     0     1     1 ...' (Live 1) with the error appended to it, then a Live 0 row. curl fails the MPTCP socket open before the transfer ever counts as live. Curl counts it live first (FailureToOpenSocket at TcpDialer.cs:382 runs inside the connect race, after the parallel loop has started the transfer). Controls: --mptcp without -Z matches once 'after N ms' is normalised, and -Z against a closed port (127.0.0.1:1) gives the same meter order in both, so the difference comes only from the synchronous MPTCP socket failure under -Z. Phase 2: ADR-0317 covers failing --mptcp with curl's lines and exit 7 but says nothing about the parallel meter order.

Reproduction, from the finding:

Run from the repository root:

```powershell
Push-Location $env:TEMP; & ./Record-CurlExchange.ps1 -Port 57920 -Curl curl.exe -OutDirectory $env:TEMP/mp-curl -CurlArgs @('--mptcp','-Z','http://127.0.0.1:57920/'); & ./Record-CurlExchange.ps1 -Port 57920 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP/mp-cand -CurlArgs @('--mptcp','-Z','http://127.0.0.1:57920/'); Pop-Location; Get-Content $env:TEMP/mp-cand/stderr.txt -TotalCount 1
```

- Expected: First stderr line: curl: (7) Failed to connect to 127.0.0.1:57920 after 0 ms: Could not connect to server
- Actual: First stderr line: DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed (error comes later, inside a Live 1 meter row)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.

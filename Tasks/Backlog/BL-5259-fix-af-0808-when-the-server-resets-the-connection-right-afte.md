---
id: BL-5259
title: Fix AF-0808: When the server resets the connection right after accepting it, Curl prints 'Recv failure: Connection was aborted' where curl prints 'Connection was reset'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5259 — Fix AF-0808: When the server resets the connection right after accepting it, Curl prints 'Recv failure: Connection was aborted' where curl prints 'Connection was reset'

## Goal

The defect the audit office reported as AF-0808 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0808 (Medium, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:113`

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:113`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Record-CurlExchange.ps1 -Reset (the server closes each accepted connection with zero linger, which sends an RST) with args -sS http://127.0.0.1:PORT/. In 4 of 4 runs (scratch probes resetconn, reset1, reset2, reset3), both exited 56: curl's stderr was `curl: (56) Recv failure: Connection was reset` and Curl's was `curl: (56) Recv failure: Connection was aborted`. StreamConnection.ReadAsync is meant to reword a WSAECONNABORTED read as a reset on Windows when the last write found no unread bytes or FIN (ReportsAbortedReadAsReset, lines 53 and 113). Its remarks name 'a peer that resets ... right after accepting' as the case it covers, but the wording still comes out as 'aborted'. I could not find where the rewording is lost on the HTTP path. Phase 2: ADR-0419 decides that this exact case (Record-CurlExchange.ps1 -Reset on http) prints curl's 'Connection was reset' and records 15 of 15 matching runs. The audited tree does not do what that ADR describes, so the ADR does not explain the divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 53133 -Reset -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\reset-curl -CurlArgs @('-sS','http://127.0.0.1:53133/') *> $null; ./Record-CurlExchange.ps1 -Port 53133 -Reset -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\reset-cand -CurlArgs @('-sS','http://127.0.0.1:53133/') *> $null; Get-Content $env:TEMP\reset-curl\stderr.txt, $env:TEMP\reset-cand\stderr.txt
```

- Expected: curl: (56) Recv failure: Connection was reset (twice: curl, then Curl)
- Actual: curl: (56) Recv failure: Connection was reset (curl), then curl: (56) Recv failure: Connection was aborted (Curl)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.

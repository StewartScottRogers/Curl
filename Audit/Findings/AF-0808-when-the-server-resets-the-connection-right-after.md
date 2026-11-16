---
id: AF-0808
title: When the server resets the connection right after accepting it, Curl prints 'Recv failure: Connection was aborted' where curl prints 'Connection was reset'
auditor: conformance
severity: Medium
status: proposed
reason:
key: conformance:reset-after-accept:Recv-failure-wording:stderr
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0808 - When the server resets the connection right after accepting it, Curl prints 'Recv failure: Connection was aborted' where curl prints 'Connection was reset'

## Summary

Medium finding from the conformance auditor at `Curl.Networking.UnitLibrary/StreamConnection.cs:113`: When the server resets the connection right after accepting it, Curl prints 'Recv failure: Connection was aborted' where curl prints 'Connection was reset'. Reported by an auditor flagged unreliable in 2026-11-15_1610.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:113`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Record-CurlExchange.ps1 -Reset (the server closes each accepted connection with zero linger, which sends an RST) with args -sS http://127.0.0.1:PORT/. In 4 of 4 runs (scratch probes resetconn, reset1, reset2, reset3), both exited 56: curl's stderr was `curl: (56) Recv failure: Connection was reset` and Curl's was `curl: (56) Recv failure: Connection was aborted`. StreamConnection.ReadAsync is meant to reword a WSAECONNABORTED read as a reset on Windows when the last write found no unread bytes or FIN (ReportsAbortedReadAsReset, lines 53 and 113). Its remarks name 'a peer that resets ... right after accepting' as the case it covers, but the wording still comes out as 'aborted'. I could not find where the rewording is lost on the HTTP path. Phase 2: ADR-0419 decides that this exact case (Record-CurlExchange.ps1 -Reset on http) prints curl's 'Connection was reset' and records 15 of 15 matching runs. The audited tree does not do what that ADR describes, so the ADR does not explain the divergence.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 53133 -Reset -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\reset-curl -CurlArgs @('-sS','http://127.0.0.1:53133/') *> $null; ./Record-CurlExchange.ps1 -Port 53133 -Reset -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\reset-cand -CurlArgs @('-sS','http://127.0.0.1:53133/') *> $null; Get-Content $env:TEMP\reset-curl\stderr.txt, $env:TEMP\reset-cand\stderr.txt
```

- Expected: curl: (56) Recv failure: Connection was reset (twice: curl, then Curl)
- Actual: curl: (56) Recv failure: Connection was reset (curl), then curl: (56) Recv failure: Connection was aborted (Curl)

## Re-audits

## Log

- 2026-11-15: filed proposed.

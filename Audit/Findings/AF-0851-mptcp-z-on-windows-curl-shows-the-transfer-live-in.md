---
id: AF-0851
title: --mptcp -Z on Windows: Curl shows the transfer live in the parallel meter before the exit 7 error, where curl fails first and the meter shows Live 0
auditor: conformance
severity: Low
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces.
key: conformance:--mptcp:parallel-socket-open-failure-order:stderr
reproduction: none
task: BL-5371
tasks: BL-5371
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: reliable-reaudit
closed-by: 2026-11-18_0810.md
---
# AF-0851 - --mptcp -Z on Windows: Curl shows the transfer live in the parallel meter before the exit 7 error, where curl fails first and the meter shows Live 0

## Summary

Low finding from the conformance auditor at `Curl.Networking.UnitLibrary/TcpDialer.cs:382`: --mptcp -Z on Windows: Curl shows the transfer live in the parallel meter before the exit 7 error, where curl fails first and the meter shows Live 0.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:382`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 320622, case 237 (--mptcp -Z), reproduced 3 out of 3 times. Both exit 7 and send no request. curl's stderr: 'curl: (7) Failed to connect to 127.0.0.1:PORT after 0 ms: Could not connect to server', then the parallel meter header, then rows '--  --      0     0     1     0 ...'. Curl's stderr: the meter header first, then a row '--  --      0     0     1     1 ...' (Live 1) with the error appended to it, then a Live 0 row. curl fails the MPTCP socket open before the transfer ever counts as live. Curl counts it live first (FailureToOpenSocket at TcpDialer.cs:382 runs inside the connect race, after the parallel loop has started the transfer). Controls: --mptcp without -Z matches once 'after N ms' is normalised, and -Z against a closed port (127.0.0.1:1) gives the same meter order in both, so the difference comes only from the synchronous MPTCP socket failure under -Z. Phase 2: ADR-0317 covers failing --mptcp with curl's lines and exit 7 but says nothing about the parallel meter order.

## Reproduction

Run from the repository root:

```powershell
Push-Location $env:TEMP; & ./Record-CurlExchange.ps1 -Port 57920 -Curl curl.exe -OutDirectory $env:TEMP/mp-curl -CurlArgs @('--mptcp','-Z','http://127.0.0.1:57920/'); & ./Record-CurlExchange.ps1 -Port 57920 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP/mp-cand -CurlArgs @('--mptcp','-Z','http://127.0.0.1:57920/'); Pop-Location; Get-Content $env:TEMP/mp-cand/stderr.txt -TotalCount 1
```

- Expected: First stderr line: curl: (7) Failed to connect to 127.0.0.1:57920 after 0 ms: Could not connect to server
- Actual: First stderr line: DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed (error comes later, inside a Live 1 meter row)

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the reproduction's two Record-CurlExchange.ps1 commands (by full path, from the scratch folder; the given './Record-CurlExchange.ps1' does not resolve from $env:TEMP) against curl 8.22.0 Schannel (Debug-enabled libcurl) and Curl.Console Release. Both exit 7. Both stderr files start with 'curl: (7) Failed to connect to 127.0.0.1:57920 after N ms: Could not connect to server', followed by the parallel meter header and rows showing Xfers 1, Live 0. The ordering and the Live column now match curl.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces.

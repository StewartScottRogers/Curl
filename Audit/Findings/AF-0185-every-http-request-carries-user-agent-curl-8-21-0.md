---
id: AF-0185
title: Every HTTP request carries User-Agent: curl/8.21.0 (and --version says 8.21.0); the reference curl sends curl/8.22.0
auditor: conformance
severity: Medium
status: accepted
reason: 
key: conformance:User-Agent:curl-version:request
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0185 - Every HTTP request carries User-Agent: curl/8.21.0 (and --version says 8.21.0); the reference curl sends curl/8.22.0

## Summary

Medium finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:45`: Every HTTP request carries User-Agent: curl/8.21.0 (and --version says 8.21.0); the reference curl sends curl/8.22.0. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:45`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. With seed 1215104239, 178 of the 191 differing cases differ in request bytes only through this header: the 176 'request' cases plus cases 44 and 75. Once the version in the User-Agent is normalised, none of them differs in the request apart from the PROXY-line source port and the multipart boundary, which are random per run. Reduced to '-s URL': curl sends 'User-Agent: curl/8.22.0' and Curl sends 'User-Agent: curl/8.21.0'. Curl's --version first line is 'curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel'. The constant is DefaultUserAgent = "curl/8.21.0" at HttpRequestHeadFormatter.cs:45, repeated in RtspRequestFormatter.cs:26, HttpProxyTunnelOptions.cs:28 and LibcurlSourceCode.cs:34. Phase 2, explained by ADR-0433: Gap/Baselines/target.json pins 8.21.0, and the ADR notes that 8.22.0 is already out. This is a deliberate target-version choice, not a coding slip, but a server or script can tell the binaries apart against this machine's curl.

## Reproduction

Run from the repository root:

```powershell
$a=@('-s','http://127.0.0.1:50961/'); foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50961 -Curl $p[1] -OutDirectory "$env:TEMP\ac-ua\$($p[0])" -CurlArgs $a | Out-Null; $p[0] + ': ' + (Select-String -Path "$env:TEMP\ac-ua\$($p[0])\request.bin" -Pattern 'User-Agent: .*').Matches[0].Value }
```

- Expected: curl: User-Agent: curl/8.22.0, candidate: User-Agent: curl/8.22.0
- Actual: curl: User-Agent: curl/8.22.0, candidate: User-Agent: curl/8.21.0

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.

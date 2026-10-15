---
id: AF-0198
title: -d '' --crlf (and --json '' --crlf) sends Transfer-Encoding: chunked with a 0 chunk where curl sends Content-Length: 0
auditor: conformance
severity: Medium
status: closed
reason: Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces.
key: conformance:--crlf:HttpRequestFraming.OfBody:request
reproduction: none
task: BL-2191
tasks: BL-2191
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed: 2026-10-14
closed-how: reliable-reaudit
closed-by: 2026-10-14_1815.md
---
# AF-0198 - -d '' --crlf (and --json '' --crlf) sends Transfer-Encoding: chunked with a 0 chunk where curl sends Content-Length: 0

## Summary

Medium finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:294`: -d '' --crlf (and --json '' --crlf) sends Transfer-Encoding: chunked with a 0 chunk where curl sends Content-Length: 0. Reported by an auditor flagged unreliable in 2026-10-13_1822.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:294`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential case 170 (seed 202976318): `--json '' --keepalive-time 0 --crlf --retry-all-errors URL` differed in request. Dropping one option at a time reduced it to `-d '' --crlf URL` (also `--json '' --crlf URL`). `--json ''` without --crlf is the same, and `-d x --crlf` and `--json x --crlf` are the same. curl's request: `POST / HTTP/1.1|Host|User-Agent: curl/8.22.0|Accept: */*|Content-Length: 0|Content-Type: application/x-www-form-urlencoded||`. Curl's request: `...|Accept: */*|Transfer-Encoding: chunked|Content-Type: application/x-www-form-urlencoded||0||`. Cause: OfBody sets `long? length = convertLineEndings ? null : unconvertedLength;` (line 294), so every --crlf body has an unknown length and is sent chunked, even an empty one whose length is known to be 0. Phase 2: no ADR records this. The doc comment cites AF-0125 for chunking a converted -T file, not for an empty -d body.

## Reproduction

Run from the repository root:

```powershell
$a=@('-d','','--crlf','http://127.0.0.1:50972/'); foreach($p in @(@('curl','C:\Program Files\Git\ucrt64\bin\curl.exe'),@('candidate',(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path))){ & ./Record-CurlExchange.ps1 -Port 50972 -Curl $p[1] -OutDirectory "$env:TEMP\ac-crlf\$($p[0])" -CurlArgs $a | Out-Null; $p[0] + ': ' + ((Select-String -Path "$env:TEMP\ac-crlf\$($p[0])\request.bin" -Pattern '^(Content-Length|Transfer-Encoding): [^\r]*').Matches.Value -join ', ') }
```

- Expected: curl: Content-Length: 0 / candidate: Content-Length: 0
- Actual: curl: Content-Length: 0 / candidate: Transfer-Encoding: chunked

## Re-audits

- 2026-10-14 | 2026-10-14_0453.md | reproduces: no | Ran the reproduction: 'curl: Content-Length: 0' and 'candidate: Content-Length: 0', with no Transfer-Encoding. The --json '' --crlf variant also sends Content-Length: 0 from both.
- 2026-10-14 | 2026-10-14_1815.md | reproduces: no | Ran the reproduction against curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel and Curl.Console/bin/Release/net10.0/curl.exe built from this tree: 'curl: Content-Length: 0' and 'candidate: Content-Length: 0'. No Transfer-Encoding: chunked in the candidate's request.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces.

---
id: AF-0199
title: Requirement FR-053 says the default request head carries User-Agent: curl/8.21.0; Curl sends curl/8.22.0
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:Documentation/Product/Requirements.md:FR-053:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0199 - Requirement FR-053 says the default request head carries User-Agent: curl/8.21.0; Curl sends curl/8.22.0

## Summary

High finding from the truthfulness auditor at `Documentation/Product/Requirements.md:188`: Requirement FR-053 says the default request head carries User-Agent: curl/8.21.0; Curl sends curl/8.22.0.

## Evidence

Location: `Documentation/Product/Requirements.md:188`

FR-053: 'With no request options, the headers are exactly `Host`, `User-Agent: curl/8.21.0`, `Accept: */*` ... `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\n...`'. Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:45 is 'internal const string DefaultUserAgent = "curl/8.22.0";', and so are the RTSP, TFTP MASQUE, proxy-tunnel and --libcurl defaults. No non-comment line of a production project names 8.21.0, and 16 name 8.22.0. Requirements.md has 17 lines with 'User-Agent: curl/8', all stale the same way. An agent that pins a test or a fix to FR-053's exact bytes would pin the wrong header. Phase 2: explained by ADR-0479 (retarget to curl 8.22.0; decision 3.2 / BL-2127 moved the User-Agent). Its decision 4 keeps 'as curl 8.21.0 does' statements as records of past measurements, but FR-053 states what Curl sends now, so the ADR does not cover it.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Product/Requirements.md -Pattern '^\| FR-053 .*User-Agent: curl/8\.21\.0').Count; (Select-String -Path Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs -Pattern 'DefaultUserAgent = ').Line.Trim()
```

- Expected: 1, then internal const string DefaultUserAgent = "curl/8.21.0"; (or 0 when FR-053 names curl/8.22.0)
- Actual: 1
internal const string DefaultUserAgent = "curl/8.22.0";

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.

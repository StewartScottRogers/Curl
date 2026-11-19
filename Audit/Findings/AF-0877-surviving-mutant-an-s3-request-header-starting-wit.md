---
id: AF-0877
title: Surviving mutant: an S3 request header starting with CR or LF is sent instead of refused
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:FormatHead-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54:>=
task: none
tasks:
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0877 - Surviving mutant: an S3 request header starting with CR or LF is sent instead of refused

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54`: Surviving mutant: an S3 request header starting with CR or LF is sent instead of refused.

## Evidence

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54`

Line 54 refuses a header field holding CR or LF: 'if (name.AsSpan().IndexOfAny('\r', '\n') >= 0 || value.AsSpan().IndexOfAny('\r', '\n') >= 0)'. The targeted -Site run mutates the name check to '> 0' and it survived. The seeded sample mutated the value check to '> 0' and that survived too. So a name or value whose first character is CR or LF passes the guard and is written into the request head, injecting request bytes. SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException has no data row with the line break at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection.cs:54:>= -Member FormatHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 54)

## Re-audits

## Log

- 2026-11-19: filed proposed.

---
id: AF-0773
title: Surviving mutant: a -H header beginning with CR or LF can be sent whole instead of cut to its empty first line
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:FirstLine-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89:<
task: BL-5222
tasks: BL-5222
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0773 - Surviving mutant: a -H header beginning with CR or LF can be sent whole instead of cut to its empty first line

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89`: Surviving mutant: a -H header beginning with CR or LF can be sent whole instead of cut to its empty first line. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89`

Mutant 'return end < 0 ? header : header[..end];' -> 'end <= 0' survived. When the header starts with CR or LF (end == 0), the original keeps only the empty first line, so the header is ignored. The mutant keeps the whole text, so the lines after the CRLF reach ApplyCustomHeader. Build_CustomHeaders_ColonRemovesSemicolonSendsEmptyAndOnlyTheFirstLineCounts covers a line break later in the header, never at index 0. This decides which fields the CONNECT sends.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:89:< -Member FirstLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-14: filed proposed.

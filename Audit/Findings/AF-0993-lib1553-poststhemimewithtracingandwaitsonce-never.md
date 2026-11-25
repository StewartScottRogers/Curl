---
id: AF-0993
title: Lib1553_PostsTheMimeWithTracingAndWaitsOnce never checks that the MIME post is set
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs:Lib1553_PostsTheMimeWithTracingAndWaitsOnce:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0993 - Lib1553_PostsTheMimeWithTracingAndWaitsOnce never checks that the MIME post is set

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs:166`: Lib1553_PostsTheMimeWithTracingAndWaitsOnce never checks that the MIME post is set.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs:166`

Step 1. The test creates var mime = new FakeMimeBuilder(); but none of its assertions use it: they check the exit code, ProgramName, the wait count, that the TransferInfo and Debug callbacks are not null, and the abort line. Lib1553.cs:69-80 sets a part (name/value) and (CurlOption.Mimepost, mime). No assertion in the file mentions Mime, so dropping the Mimepost option or the part would pass. The 'WithTracing' part is checked only with Assert.IsNotNull(seen.Callbacks.Debug).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1551To1559Tests.cs -Pattern 'Assert.*Mime').Count
```

- Expected: At least 1: an assertion that the transfer's MIME post holds the name=value part.
- Actual: 0

## Re-audits

## Log

- 2026-11-25: filed proposed.

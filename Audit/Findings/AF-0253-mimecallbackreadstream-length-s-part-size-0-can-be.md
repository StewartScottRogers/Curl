---
id: AF-0253
title: MimeCallbackReadStream.Length's part.Size >= 0 can become > 0 with no test failing, so an empty sized callback part throws
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:Length-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39:>=
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0253 - MimeCallbackReadStream.Length's part.Size >= 0 can become > 0 with no test failing, so an empty sized callback part throws

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39`: MimeCallbackReadStream.Length's part.Size >= 0 can become > 0 with no test failing, so an empty sized callback part throws. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39`

Engine sample, seed 0: survived. Original `CanSeek && part.Size >= 0 ? part.Size : throw new NotSupportedException()`, mutated `> 0`. A seekable mime callback part of declared size 0 would throw NotSupportedException instead of reporting length 0, which changes how its Content-Length is computed. No test covers the size-0 edge.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39:>= -Member Length -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.

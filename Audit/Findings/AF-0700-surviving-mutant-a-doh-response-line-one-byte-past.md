---
id: AF-0700
title: Surviving mutant: a DoH response line one byte past the limit can be read
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/DohResponseReader.cs:ReadLineAsync-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DohResponseReader.cs:219:<
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0700 - Surviving mutant: a DoH response line one byte past the limit can be read

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`: Surviving mutant: a DoH response line one byte past the limit can be read.

## Evidence

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`

'while (next is >= 0 and not '\n' && line.Count < MaximumLineLength)' mutated to '<= MaximumLineLength' survived. MaximumLineLength is 100 * 1024. The method should return null for a line that runs past it (comment at line 212), but no test sends a line at the boundary, so the limit can be off by one.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:219:< -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.

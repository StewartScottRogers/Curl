---
id: AF-0820
title: Surviving mutant: lib1576's read callback can send an unfilled buffer when it is exactly the body's length
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:Read-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79:>=
task: BL-5274
tasks: BL-5274
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0820 - Surviving mutant: lib1576's read callback can send an unfilled buffer when it is exactly the body's length

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79`: Surviving mutant: lib1576's read callback can send an unfilled buffer when it is exactly the body's length.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79`

`if (buffer.Length >= Body.Length)` changed to `>` survived. With a buffer exactly the body's length, the body is not copied but Body.Length is still returned, so the upload sends whatever the buffer held. Lib1576_Read_CopiesTheBodyOnEveryCallUnlessTheBufferIsTooSmall only uses 100- and 10-byte buffers. This is the same boundary as AF-0800 in Lib757.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1576.cs:79:>= -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.

---
id: AF-0800
title: Surviving mutant: lib757's read callback can send nothing when the buffer is exactly the part's length
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:Read-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29:<
task: BL-5251
tasks: BL-5251
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_0514.md
---
# AF-0800 - Surviving mutant: lib757's read callback can send nothing when the buffer is exactly the part's length

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29`: Surviving mutant: lib757's read callback can send nothing when the buffer is exactly the part's length.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary (one of the three largest): 'if (buffer.Length < Data.Length)' -> 'if (buffer.Length <= Data.Length)' in Read survived. A buffer of exactly 24 bytes, the length of '<title>fun-times</title>', then gets 0 bytes instead of the part, so the uploaded MIME part goes out empty. No test calls Read with a buffer of exactly Data.Length, so the 'whole of Data when it fits' boundary the doc comment states is unchecked.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib757.cs:29:< -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-16 | 2026-11-16_0514.md | reproduces: no | Ran the -Site command: Lib757.cs:29 `buffer.Length < Data.Length` -> `<=` was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

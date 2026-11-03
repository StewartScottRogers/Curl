---
id: AF-0605
title: Surviving mutant: a header write starting with a bare LF can be taken as one line without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:LengthOfFirstLine-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89:<
task: BL-4769
tasks: BL-4769
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0605 - Surviving mutant: a header write starting with a bare LF can be taken as one line without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89`: Surviving mutant: a header write starting with a bare LF can be taken as one line without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89`

lineFeed < 0 -> <= 0 survived (sample and targeted rerun): when the first byte is '\n' the whole buffer is treated as the first line, changing how header lines are split for the header tee.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs:89:< -Member LengthOfFirstLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.

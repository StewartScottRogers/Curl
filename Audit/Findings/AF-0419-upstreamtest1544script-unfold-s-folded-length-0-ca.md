---
id: AF-0419
title: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-24.
key: quality:Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:Unfold-gt:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:>
task: BL-3468
tasks: BL-3468
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0419 - UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space.

## Evidence

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'line = folded.Length > 0 ? folded + " " + line : line;' -> '>= 0'. With no pending fold, the line becomes " " + line. Its first character is then never '#', so comment lines are no longer dropped, and the option patterns the script matches see a shifted line. The script decides test1544's verdict, and no test notices the change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.

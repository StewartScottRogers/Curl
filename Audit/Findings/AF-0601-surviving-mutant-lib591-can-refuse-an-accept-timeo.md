---
id: AF-0601
title: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:TryParseAcceptTimeout-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66:>=
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0601 - Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`

seconds is >= 0 -> > 0 survived: an argument of '0' is then refused as not a timeout, a decision the port makes from its command line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66:>= -Member TryParseAcceptTimeout -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.

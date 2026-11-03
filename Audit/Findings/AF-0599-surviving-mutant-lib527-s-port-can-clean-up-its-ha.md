---
id: AF-0599
title: Surviving mutant: lib527's port can clean up its handles on the opposite outcome without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:CleansUpTheHandles-ne:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:!=
task: BL-4763
tasks: BL-4763
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0599 - Surviving mutant: lib527's port can clean up its handles on the opposite outcome without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`: Surviving mutant: lib527's port can clean up its handles on the opposite outcome without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

steps.Result != CurlCode.Ok -> == survived for testNumber 527: the port then cleans up its handles after success and leaves them after failure, the reverse of upstream lib526.c's TEST_HANG/527 branch.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:!= -Member CleansUpTheHandles -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.

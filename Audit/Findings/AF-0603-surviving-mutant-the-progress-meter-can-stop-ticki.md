---
id: AF-0603
title: Surviving mutant: the progress meter can stop ticking when only one of its outputs is set without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:TransferProgressRecorder-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174:||
task: BL-4767
tasks: BL-4767
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0603 - Surviving mutant: the progress meter can stop ticking when only one of its outputs is set without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174`: Surviving mutant: the progress meter can stop ticking when only one of its outputs is set without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174`

writeLive is not null || progressBar is not null -> && survived (sample and targeted rerun, 23 baseline-failing tests left out): with only the live meter or only the bar, no timer is created, so the meter is not redrawn each second during a transfer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174:|| -Member TransferProgressRecorder -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command (3 unmutated-failing Engine tests excluded): TransferProgressRecorder.cs:174 || resolved to line 174, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

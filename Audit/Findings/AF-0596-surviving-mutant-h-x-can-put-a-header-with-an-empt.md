---
id: AF-0596
title: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:AddGiven-gt:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134:>
task: BL-4760
tasks: BL-4760
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0596 - Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`

Drawn library. if (colon > 0) -> colon >= 0 survived: a -H line starting with ':' is then added as a field with an empty name, so the generated C# or PowerShell holds a header curl never sends. From_TheOptionsThatSetFields_GiveThoseFieldsUnlessHGaveThem covers 'Host:' and 'NoSeparator' but no leading colon.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134:> -Member AddGiven -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: GeneratedRequest.cs:134 > resolved to line 134, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

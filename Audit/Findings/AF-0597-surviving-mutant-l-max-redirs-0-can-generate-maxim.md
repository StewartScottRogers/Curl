---
id: AF-0597
title: Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:MaximumRedirection-lt:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76:<
task: BL-4761
tasks: BL-4761
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0597 - Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing

## Summary

High finding from the quality auditor at `Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76`: Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76`

Drawn library. request.MaxRedirects < 0 -> <= 0 survived: with FollowRedirects and MaxRedirects 0 the generated script follows unlimited redirects instead of none, so --powershell writes different bytes. For_FollowingRedirects_SetsMaximumRedirection checks only 7 and -1.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76:< -Member MaximumRedirection -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: PowerShellScript.cs:76 < resolved to line 76, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

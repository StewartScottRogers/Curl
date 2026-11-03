---
id: AF-0597
title: Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:MaximumRedirection-lt:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76:<
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

## Log

- 2026-11-03: filed proposed.

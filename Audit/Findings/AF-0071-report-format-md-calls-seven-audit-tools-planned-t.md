---
id: AF-0071
title: Report-Format.md calls seven audit tools 'planned' that already exist in Audit/Tools
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).
key: truthfulness:Audit/Instructions/Report-Format.md:planned-tools:false-statement
reproduction: none
task: BL-1690
tasks: BL-1690
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: consecutive
closed-by: 2026-10-08_0748.md, 2026-10-08_2315.md
---
# AF-0071 - Report-Format.md calls seven audit tools 'planned' that already exist in Audit/Tools

## Summary

Low finding from the truthfulness auditor at `Audit/Instructions/Report-Format.md:5`: Report-Format.md calls seven audit tools 'planned' that already exist in Audit/Tools. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Audit/Instructions/Report-Format.md:5`

Report-Format.md marks Write-AuditFindings.ps1 (BL-1016), Write-AuditScorecard.ps1 (BL-1017), Get-AuditorFingerprint.ps1 (BL-1002), Measure-Performance.ps1 (BL-1007), Measure-FactoryProcess.ps1 (BL-1008), Invoke-MutationTest.ps1 (BL-1004), Fuzz/Fuzz.cs (BL-1005) and Invoke-DifferentialConformance.ps1 (BL-1006) as '(planned ...)'. All of them are in Audit/Tools/. The intent is written as if the tools did not exist, so a reader may think the report block is consumed by nothing yet.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Audit/Instructions/Report-Format.md -SimpleMatch '(planned').Count; Test-Path Audit/Tools/Write-AuditFindings.ps1
```

- Expected: 0, True
- Actual: 7, True

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | The count of '(planned' in Audit/Instructions/Report-Format.md is 0; Test-Path Audit/Tools/Write-AuditFindings.ps1 is True. The tools are no longer called planned.
- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | The count of '(planned' in Report-Format.md is 0; Test-Path Audit/Tools/Write-AuditFindings.ps1 is True. One leftover ', planned BL-1002' for Get-AuditorFingerprint.ps1 on line 37 is outside this reproduction, so I filed it as a new finding.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).

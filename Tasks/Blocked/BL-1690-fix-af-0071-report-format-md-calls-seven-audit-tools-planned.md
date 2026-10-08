---
id: BL-1690
title: Fix AF-0071: Report-Format.md calls seven audit tools 'planned' that already exist in Audit/Tools
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: []
lane: no
requirement: none
created: 2026-10-08
completed:
---
# BL-1690 — Fix AF-0071: Report-Format.md calls seven audit tools 'planned' that already exist in Audit/Tools

## Goal

The defect the audit office reported as AF-0071 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0071 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0071-report-format-md-calls-seven-audit-tools-planned-t.md`.

Location: `Audit/Instructions/Report-Format.md:5`

Location: `Audit/Instructions/Report-Format.md:5`

Report-Format.md marks Write-AuditFindings.ps1 (BL-1016), Write-AuditScorecard.ps1 (BL-1017), Get-AuditorFingerprint.ps1 (BL-1002), Measure-Performance.ps1 (BL-1007), Measure-FactoryProcess.ps1 (BL-1008), Invoke-MutationTest.ps1 (BL-1004), Fuzz/Fuzz.cs (BL-1005) and Invoke-DifferentialConformance.ps1 (BL-1006) as '(planned ...)'. All of them are in Audit/Tools/. The intent is written as if the tools did not exist, so a reader may think the report block is consumed by nothing yet.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Audit/Instructions/Report-Format.md -SimpleMatch '(planned').Count; Test-Path Audit/Tools/Write-AuditFindings.ps1
```

- Expected: 0, True
- Actual: 7, True

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07 (lane 8): The only file this fix changes is the audit office's report-format instructions, an audit path. Dark factory lanes may not read or change audit paths (CLAUDE.md "Audit office", ADR-0267); the audit guard hook refused the lane. Marked `lane: no` so only an interactive session, working on the `audit` branch, takes it.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. The fix edits an audit-office file, which lanes may not touch (ADR-0267); marked lane: no for an interactive session on the audit branch
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Done on the audit branch; waiting on PR #72's CI before merging to master

---
id: BL-1877
title: Fix AF-0127: Report-Format.md still calls Get-AuditorFingerprint.ps1 'planned BL-1002' though it exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: []
requirement: none
created: 2026-10-09
completed:
---
# BL-1877 — Fix AF-0127: Report-Format.md still calls Get-AuditorFingerprint.ps1 'planned BL-1002' though it exists

## Goal

The defect the audit office reported as AF-0127 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0127 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0127-report-format-md-still-calls-get-auditorfingerprin.md`.

Location: `Audit/Instructions/Report-Format.md:37`

Location: `Audit/Instructions/Report-Format.md:37`

Line 37: 'The auditor fingerprint the prompt gives (64 lowercase hex characters, from `Audit/Tools/Get-AuditorFingerprint.ps1`, planned BL-1002)'. Audit/Tools/Get-AuditorFingerprint.ps1 exists. This is the last 'planned' left after AF-0071's fix: AF-0071's reproduction searches only for '(planned', and this line has ', planned'.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Audit/Instructions/Report-Format.md -SimpleMatch 'planned BL-1002').LineNumber; Test-Path Audit/Tools/Get-AuditorFingerprint.ps1
```

- Expected: No line number (the word planned removed), then True.
- Actual: 37, then True.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.

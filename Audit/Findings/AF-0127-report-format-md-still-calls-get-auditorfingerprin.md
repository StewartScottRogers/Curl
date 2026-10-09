---
id: AF-0127
title: Report-Format.md still calls Get-AuditorFingerprint.ps1 'planned BL-1002' though it exists
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Audit/Instructions/Report-Format.md:Get-AuditorFingerprint:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0127 - Report-Format.md still calls Get-AuditorFingerprint.ps1 'planned BL-1002' though it exists

## Summary

Low finding from the truthfulness auditor at `Audit/Instructions/Report-Format.md:37`: Report-Format.md still calls Get-AuditorFingerprint.ps1 'planned BL-1002' though it exists. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Audit/Instructions/Report-Format.md:37`

Line 37: 'The auditor fingerprint the prompt gives (64 lowercase hex characters, from `Audit/Tools/Get-AuditorFingerprint.ps1`, planned BL-1002)'. Audit/Tools/Get-AuditorFingerprint.ps1 exists. This is the last 'planned' left after AF-0071's fix: AF-0071's reproduction searches only for '(planned', and this line has ', planned'.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Audit/Instructions/Report-Format.md -SimpleMatch 'planned BL-1002').LineNumber; Test-Path Audit/Tools/Get-AuditorFingerprint.ps1
```

- Expected: No line number (the word planned removed), then True.
- Actual: 37, then True.

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the reproduction. Report-Format.md line 37 still contains 'planned BL-1002', and Test-Path Audit/Tools/Get-AuditorFingerprint.ps1 returned True.

## Log

- 2026-10-08: filed proposed.

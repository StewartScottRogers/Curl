---
id: BL-3465
title: Set aside a new audit finding that another auditor files in a planted defect's file
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3465 — Set aside a new audit finding that another auditor files in a planted defect's file

## Goal

A new finding from any auditor whose location is in a file the seeder planted a defect in, and that is not a catch for the plant's own auditor, is set aside with a note in the audit's report rather than filed as a proposed finding.

## Context

Audit 2026-10-24_0845 (run folder `Z:\repos\Curl.audit\20261024-084551`) filed AF-0416 (High, quality): `HandshakeMessageReader` slices before its length check. That is the security auditor's planted defect in `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs` (the removed `buffer.Length < total` check), found by the quality auditor. `Test-Catch` in `Audit/Tools/Write-AuditFindings.ps1` (line 513) only matches the plant's own auditor, so a different auditor's report of the same defect falls through and is filed as a real finding. Re-audit verdicts that overlap a plant are already set aside (`Get-OverlappingPlant`, ADR-0422); new findings are not.

## Acceptance criteria

- [ ] `Invoke-WriteFindings` sets aside a new finding whose location is in a planted file (for a code plant) when `Test-Catch` does not match it for its own auditor, and writes no `AF-*.md` for it.
- [ ] Each set-aside finding is listed in the run's report directory (auditor, plant id, title), and the summary line counts them.
- [ ] A new finding in another file of the same project is still filed.
- [ ] The script's self-test gains a check for each of the two cases above, and passes.
- [ ] Done on the `audit` branch; its pull request is merged once CI passes on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-24: Created.

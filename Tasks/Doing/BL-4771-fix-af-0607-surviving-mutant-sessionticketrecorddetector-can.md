---
id: BL-4771
title: Fix AF-0607: Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4771 — Fix AF-0607: Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing

## Goal

The defect the audit office reported as AF-0607 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0607 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0607-surviving-mutant-sessionticketrecorddetector-can-c.md`.

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104`

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104`

tickets < _plaintextLengths.Count -> <= survived (sample and targeted rerun): the loop then asks RunEndAccountingFor for one more run than there are records, changing the ticket count and the CloseNotifyFollows decision the -v TLS lines are written from.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104:< -Member CountTicketRecords -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

---
id: BL-2767
title: Fix AF-0256: CountTicketRecords' _finished = true can become false with no test failing, so tickets are counted again after the first read
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2767 — Fix AF-0256: CountTicketRecords' _finished = true can become false with no test failing, so tickets are counted again after the first read

## Goal

The defect the audit office reported as AF-0256 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0256 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0256-countticketrecords-finished-true-can-become-false.md`.

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92`

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `_finished = true;`, mutated `false`. The doc says 'after it, nothing more is observed'. With the mutant, later plaintext reads count ticket records again, which changes the session-ticket decisions built on the count. No test makes a second call.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92:true -Member CountTicketRecords -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

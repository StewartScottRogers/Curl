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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The mutant was equivalent. With `tickets <= Count`, the extra call `RunEndAccountingFor(Count, read)` summed no record and returned -1 for any read other than 0, which `CountTicketRecords` already rules out, so no test could tell the two apart. The fix is in production code: `RunEndAccountingFor` now reads the run's first record without a bounds check (a run holds at least one record), so asking past the queue throws. Behaviour for every index inside the queue is unchanged.
- Mutation check: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand at SessionTicketRecordDetector.cs:104. With `<=`, `CountTicketRecords_ReadStoppingInsideARecord_CountsNone` and `CountTicketRecords_NoRunOfRecordsAccountsForTheRead_CountsNone` fail (2 failed, 9 passed). With the line restored they all pass. No new test was needed, so `touches` stays at the library.
- `dotnet build`: 0 warnings, 0 errors. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` ran the fast tests of all 21 projects that reach the library: all passed, Curl.Networking.UnitTests with 4018 passed and 34 skipped. It reported 0 failing members. The change is platform-neutral (in-memory code only).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. SessionTicketRecordDetector's tickets <= Count mutant now throws and is killed by the detector's no-run tests

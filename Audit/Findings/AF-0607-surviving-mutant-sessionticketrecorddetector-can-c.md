---
id: AF-0607
title: Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:CountTicketRecords-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104:<
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
# AF-0607 - Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104`: Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104`

tickets < _plaintextLengths.Count -> <= survived (sample and targeted rerun): the loop then asks RunEndAccountingFor for one more run than there are records, changing the ticket count and the CloseNotifyFollows decision the -v TLS lines are written from.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104:< -Member CountTicketRecords -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.

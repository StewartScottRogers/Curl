---
id: AF-0607
title: Surviving mutant: SessionTicketRecordDetector can count one ticket record past its list without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:CountTicketRecords-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:104:<
task: BL-4771
tasks: BL-4771
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: SessionTicketRecordDetector.cs:104 < resolved to line 104, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

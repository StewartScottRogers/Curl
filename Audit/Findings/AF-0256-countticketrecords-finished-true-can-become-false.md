---
id: AF-0256
title: CountTicketRecords' _finished = true can become false with no test failing, so tickets are counted again after the first read
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:CountTicketRecords-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92:true
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0256 - CountTicketRecords' _finished = true can become false with no test failing, so tickets are counted again after the first read

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92`: CountTicketRecords' _finished = true can become false with no test failing, so tickets are counted again after the first read. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `_finished = true;`, mutated `false`. The doc says 'after it, nothing more is observed'. With the mutant, later plaintext reads count ticket records again, which changes the session-ticket decisions built on the count. No test makes a second call.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:92:true -Member CountTicketRecords -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.

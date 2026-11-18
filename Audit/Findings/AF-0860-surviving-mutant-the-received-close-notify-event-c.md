---
id: AF-0860
title: Surviving mutant: the received close_notify event can be reported as sent
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:CloseNotifyReceived-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51:false
task: BL-5502
tasks: BL-5502
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0860 - Surviving mutant: the received close_notify event can be reported as sent

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51`: Surviving mutant: the received close_notify event can be reported as sent. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51`

Mutant 'Sent = false,' -> 'Sent = true,' in the static CloseNotifyReceived TlsMessageEvent survived the whole Curl.Networking.UnitTests run (mutation-Curl.Networking.UnitLibrary.json, seed 435403). Line 146 reports the event with events.ReportTlsMessage(CloseNotifyReceived), and the verbose writer renders Sent as (OUT) instead of (IN). So curl -v would print the server's close_notify as one curl sent, which changes stderr bytes, and no test pins the direction.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51:false -Member CloseNotifyReceived -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-18: filed proposed.

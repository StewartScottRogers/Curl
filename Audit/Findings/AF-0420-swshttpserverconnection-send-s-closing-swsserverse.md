---
id: AF-0420
title: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:Send-true:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0420 - SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close.

## Evidence

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'pendingSends.Add(new SwsServerSend([], serverBusyUntil, true));' -> 'false'. The third argument is isClose: the send queued after a closing reply (swsclose, close after the reply) is what ends the connection. With false, the stand-in server no longer closes after such a reply. No test checks that the connection ends.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.

---
id: AF-0955
title: Surviving mutant: a connection reset can be taken for the peer's FIN (HasUnreadEnd catch returns true)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/StreamConnection.cs:HasUnreadEnd-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/StreamConnection.cs:106:false
task: BL-5724
tasks: BL-5724
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0955 - Surviving mutant: a connection reset can be taken for the peer's FIN (HasUnreadEnd catch returns true)

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/StreamConnection.cs:106`: Surviving mutant: a connection reset can be taken for the peer's FIN (HasUnreadEnd catch returns true). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:106`

Lines 98-107: HasUnreadEnd peeks one byte, and its catch (SocketException) { return false; } exists because 'a reset fails the peek and is not an end'. Mutated to return true it survived. The method carries [ExcludeFromCodeCoverage] ('measured by the Integration run'), so no fast test reaches it: a reset would be reported as an orderly end of the connection.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:106:false -Member HasUnreadEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.

---
id: AF-0805
title: Surviving mutant: a socket from CURLOPT_OPENSOCKETFUNCTION can be treated as the connection's own, changing when the close-socket callback runs
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:OpenSocketAsync-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249:false
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0805 - Surviving mutant: a socket from CURLOPT_OPENSOCKETFUNCTION can be treated as the connection's own, changing when the close-socket callback runs

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249`: Surviving mutant: a socket from CURLOPT_OPENSOCKETFUNCTION can be treated as the connection's own, changing when the close-socket callback runs.

## Evidence

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249`

The seed 770877 sample of Curl.Engine.UnitLibrary: '({ } open, _) => (open(CurlSocketType.IpConnection, address), false)' -> '..., true)' in SocketFor (the tool named the member OpenSocketAsync) survived. IsTheConnectionsOwnSocket then becomes true for the application's own socket. SocketCallbackConnection.DisposeAsync (lines 79-85) then calls the close-socket callback before the inner connection is disposed rather than after, so the order the application's callbacks see changes. No test checks the close order for a socket the open-socket callback supplied.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:249:false -Member OpenSocketAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.

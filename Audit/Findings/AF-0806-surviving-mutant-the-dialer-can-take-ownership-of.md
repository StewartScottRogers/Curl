---
id: AF-0806
title: Surviving mutant: the dialer can take ownership of an application-supplied socket and close it itself
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/TcpDialer.cs:DialSuppliedSocketAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpDialer.cs:346:false
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
# AF-0806 - Surviving mutant: the dialer can take ownership of an application-supplied socket and close it itself

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpDialer.cs:346`: Surviving mutant: the dialer can take ownership of an application-supplied socket and close it itself.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:346`

The seed 770877 sample of Curl.Networking.UnitLibrary (one of the three largest): 'new Socket(new SafeSocketHandle(handle, ownsHandle: false))' -> 'ownsHandle: true' in DialSuppliedSocketAsync survived. The managed Socket then closes the handle the application supplied (CURLOPT_OPENSOCKETFUNCTION) when it is disposed. That handle is the application's to close through its close-socket callback, so the result is a double close or a handle closed under it. No test checks that a supplied handle is still open after the connection is disposed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpDialer.cs:346:false -Member DialSuppliedSocketAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.

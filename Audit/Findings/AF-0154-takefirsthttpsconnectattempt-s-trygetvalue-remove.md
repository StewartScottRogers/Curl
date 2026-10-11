---
id: AF-0154
title: TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:TakeFirstHttpsConnectAttempt-and:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118:&&
task: none
tasks:
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0154 - TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118`: TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures: `_firstHttpsConnectAttempts.TryGetValue(target, out var attempt) && _firstHttpsConnectAttempts.Remove(target) ? attempt : null` mutated to `||` survived. The comment above the method says the attempt is 'taken once'. With `||`, a found entry short-circuits Remove, so it stays in the table. A later connect to the same ConnectTarget gets the stale attempt again. ReportHttpsConnectAttemptFailure then calls attempt.Fail(exitCode) instead of writing the HTTPS-connect filter's failure lines, which changes the -v trace. No test connects to one target a third time, or checks that the entry is gone after it is taken.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118:&& -Member TakeFirstHttpsConnectAttempt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.

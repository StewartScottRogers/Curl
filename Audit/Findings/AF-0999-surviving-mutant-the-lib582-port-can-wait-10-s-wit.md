---
id: AF-0999
title: Surviving mutant: the lib582 port can wait 10 s with no sockets and 100 ms with sockets (== -> !=)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:TurnAsync-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177:==
task: BL-5993
tasks: BL-5993
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0999 - Surviving mutant: the lib582 port can wait 10 s with no sockets and 100 ms with sockets (== -> !=)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177`: Surviving mutant: the lib582 port can wait 10 s with no sockets and 100 ms with sockets (== -> !=).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177`

Mutant ': readSockets.Count + writeSockets.Count == 0 ? IdleTimeoutMilliseconds' -> '!= 0' survived. The select timeout decision swaps IdleTimeoutMilliseconds (100) and SelectTimeoutMilliseconds (10_000): with no socket to watch the port waits 10 s a turn instead of 100 ms, enough to reach the hang timeout. No test checks the timeout passed to select_test.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177:== -Member TurnAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.

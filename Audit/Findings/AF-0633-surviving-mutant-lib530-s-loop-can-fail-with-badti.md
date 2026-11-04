---
id: AF-0633
title: Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:ActOnTimerAsync-or:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253:||
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0633 - Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253`: Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, '|| await SocketActionAsync(multi, CurlMultiSockets.Timeout, ...) == CurlMultiCode.Ok' -> '&&'. The condition becomes 'timerDeadline is null || (RemainingMicroseconds() > 0 && action == Ok)'. An expired timer then never gets its CURL_SOCKET_TIMEOUT action, and the run sets LibTestErrorCodes.BadTimeout, which changes the program's exit code, while a live timer runs an extra action. No test drives an expired timer through ActOnTimerAsync.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253:|| -Member ActOnTimerAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.

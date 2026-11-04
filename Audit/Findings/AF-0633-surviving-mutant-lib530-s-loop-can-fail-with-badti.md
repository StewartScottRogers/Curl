---
id: AF-0633
title: Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:ActOnTimerAsync-or:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253:||
task: BL-4812
tasks: BL-4812
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_0643.md
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

- 2026-11-04 | 2026-11-04_0643.md | reproduces: no | Ran the -Site command: outcome killed, resolvedLine 253. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

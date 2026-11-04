---
id: BL-4812
title: Fix AF-0633: Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4812 — Fix AF-0633: Surviving mutant: lib530's loop can fail with BadTimeout instead of running the expired timer's socket action, and no test fails

## Goal

The defect the audit office reported as AF-0633 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0633 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0633-surviving-mutant-lib530-s-loop-can-fail-with-badti.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, '|| await SocketActionAsync(multi, CurlMultiSockets.Timeout, ...) == CurlMultiCode.Ok' -> '&&'. The condition becomes 'timerDeadline is null || (RemainingMicroseconds() > 0 && action == Ok)'. An expired timer then never gets its CURL_SOCKET_TIMEOUT action, and the run sets LibTestErrorCodes.BadTimeout, which changes the program's exit code, while a live timer runs an extra action. No test drives an expired timer through ActOnTimerAsync.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:253:|| -Member ActOnTimerAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

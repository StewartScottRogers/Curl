---
id: BL-2047
title: Fix AF-0154: TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2047 — Fix AF-0154: TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once

## Goal

The defect the audit office reported as AF-0154 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0154 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0154-takefirsthttpsconnectattempt-s-trygetvalue-remove.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118`

Location: `Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures: `_firstHttpsConnectAttempts.TryGetValue(target, out var attempt) && _firstHttpsConnectAttempts.Remove(target) ? attempt : null` mutated to `||` survived. The comment above the method says the attempt is 'taken once'. With `||`, a found entry short-circuits Remove, so it stays in the table. A later connect to the same ConnectTarget gets the stale attempt again. ReportHttpsConnectAttemptFailure then calls attempt.Fail(exitCode) instead of writing the HTTPS-connect filter's failure lines, which changes the -v trace. No test connects to one target a third time, or checks that the entry is gone after it is taken.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs:118:&& -Member TakeFirstHttpsConnectAttempt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.

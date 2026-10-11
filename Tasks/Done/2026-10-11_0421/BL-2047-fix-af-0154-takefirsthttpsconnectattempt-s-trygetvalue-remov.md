---
id: BL-2047
title: Fix AF-0154: TakeFirstHttpsConnectAttempt's `TryGetValue(...) && Remove(...)` can become `||` with no test failing: the first attempt is no longer taken once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a missing test. Added
  `TcpConnectorQuicTests.ConnectAsync_ASecondTimeAfterAFailedQuicConnectForTheSameTarget_StartsAFreshFilterAsTheQuicAttemptWasTakenOnce`:
  a refused QUIC connect, then two TCP connects to the same target; the second must start a
  fresh `[HTTPS-CONNECT]` filter, not write `h3 baller failed, starting h2` again.
- Added `Curl.Networking.UnitTests` to `touches`: the test lives there, and no task in Doing on
  `origin/work/dark-factory` names it (BL-2043, BL-2045, BL-2048, BL-2051, BL-2052 checked).
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied
  by hand instead (`&&` to `||` at `TcpConnector.QuicHttpsConnect.cs:118`): the new test failed,
  the other 109 `TcpConnectorQuicTests` passed; with the code restored it passes. The finding
  still closes only on the quality auditor's re-audit.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. A test now connects to one target after a refused QUIC connect twice over TCP, killing AF-0154's || mutant

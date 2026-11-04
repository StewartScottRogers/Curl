---
id: BL-4818
title: Fix AF-0639: Surviving mutant: the hand-built TLS connection can forget it already sent close_notify and send it again, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/CloseNotifyOnDisposeTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4818 — Fix AF-0639: Surviving mutant: the hand-built TLS connection can forget it already sent close_notify and send it again, and no test fails

## Goal

The defect the audit office reported as AF-0639 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0639 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0639-surviving-mutant-the-hand-built-tls-connection-can.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, '_tlsEnded = true;' -> '_tlsEnded = false;' in SendCloseNotifyAsync. The guard at line 147 then lets a second SendCloseNotifyAsync (dispose after an explicit shutdown) run ShutdownAsync again, putting a second close_notify alert on the wire. HandBuiltTlsConnectionCloseNotifyTests checks a cleared connection but never a second shutdown.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152:true -Member SendCloseNotifyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `DisposeAsync_OfAHandBuiltTlsConnectionCalledTwice_TriesToSendCloseNotifyOnlyOnce` to `Curl.Networking.UnitTests/CloseNotifyOnDisposeTests.cs` (added to touches: the test project of the touched library), the twin of AF-0606's SslStream test. The hand-built TLS streams refuse a second `ShutdownAsync` themselves (`shutdownSent`, then `ObjectDisposedException`), so the wire cannot show the mutant; each attempt starts its close_notify bound on the connection's `TimeProvider`, which `TimerCountingTimeProvider` counts. No production change.
- Reproduction: the lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`_tlsEnded = false;` at HandBuiltTlsConnection.cs:152): the new test failed (1 of 5 in the class), and passes with the line restored, so the mutant is killed. Build clean, fast tests green (0 failures).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A second dispose of a HandBuiltTlsConnection is pinned to try close_notify once; AF-0639's mutant is killed

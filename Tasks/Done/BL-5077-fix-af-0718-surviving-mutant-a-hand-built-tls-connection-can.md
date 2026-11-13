---
id: BL-5077
title: Fix AF-0718: Surviving mutant: a hand-built TLS connection can try a second close_notify after ShutDownSendingAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5077 — Fix AF-0718: Surviving mutant: a hand-built TLS connection can try a second close_notify after ShutDownSendingAsync

## Goal

The defect the audit office reported as AF-0718 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0718 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0718-surviving-mutant-a-hand-built-tls-connection-can-t.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114`

Mutant '_tlsEnded = true;' -> '_tlsEnded = false;' survived. DisposeAsync's remarks say close_notify is sent 'unless ClearTlsAsync or ShutDownSendingAsync already ended TLS'. With the mutant, DisposeAsync writes another TLS record after the send side was shut down. No test disposes a connection after ShutDownSendingAsync and checks the wire.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114:true -Member ShutDownSendingAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The audit guard refuses a lane `Audit/Tools`, so the mutation script was not run. By hand: with line 114 flipped to `_tlsEnded = false;` the new `CloseNotifyOnDisposeTests.DisposeAsync_OfAHandBuiltTlsConnectionAfterSendingWasShutDown_TriesNoSecondCloseNotify` fails (killed); restored, it passes. The hand-built streams ignore a second shutdown, so the wire cannot tell; only a timer-counting clock can. `HandBuiltTlsConnectionCloseNotifyTests` also pins one TLS record on the wire after ShutDownSendingAsync and dispose.
- Criterion 1 is ticked on that by-hand check; the re-audit confirms it.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added a clock-counting test that kills the AF-0718 mutant (verified by hand; the audit script is off limits to lanes)

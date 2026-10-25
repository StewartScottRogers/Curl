---
id: BL-3471
title: Fix AF-0422: StreamConnection.DiscardUnreadAndShutDownSocketUnder's Socket.Available > 0 can become >= 0 with no test failing, so closing a connection blocks in Receive
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3471 — Fix AF-0422: StreamConnection.DiscardUnreadAndShutDownSocketUnder's Socket.Available > 0 can become >= 0 with no test failing, so closing a connection blocks in Receive

## Goal

The defect the audit office reported as AF-0422 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0422 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0422-streamconnection-discardunreadandshutdownsocketund.md`.

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:156`

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:156`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'while (network.Socket.Available > 0 && network.Socket.Receive(discard) > 0)' -> '>= 0'. With >= 0, the loop calls the blocking Socket.Receive even when nothing is waiting. Shutting down a connection whose peer keeps it open then waits until the peer sends or closes, which hangs curl at the end of a transfer. No test covers the drain on a real NetworkStream.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:156:> -Member DiscardUnreadAndShutDownSocketUnder -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

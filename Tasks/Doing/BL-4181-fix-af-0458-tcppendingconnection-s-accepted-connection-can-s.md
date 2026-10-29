---
id: BL-4181
title: Fix AF-0458: TcpPendingConnection's accepted connection can stop owning its socket (ownsSocket: true -> false) with no test failing, so an accepted data connection is never closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4181 — Fix AF-0458: TcpPendingConnection's accepted connection can stop owning its socket (ownsSocket: true -> false) with no test failing, so an accepted data connection is never closed

## Goal

The defect the audit office reported as AF-0458 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0458 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0458-tcppendingconnection-s-accepted-connection-can-sto.md`.

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99`

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99`

Line 99: `new NetworkStream(accepted, ownsSocket: true),` in AcceptStreamConnectionAsync. With ownsSocket false, disposing the StreamConnection leaves the accepted socket open. The peer of an active-mode (PORT/EPRT) data connection then never sees a FIN, so an upload's end is not signalled and the socket leaks. The mutation run (seed 0) left it surviving. The method is [ExcludeFromCodeCoverage] (`measured by the Integration run`), so no fast test reaches it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99:true -Member NetworkStream -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a loopback accept test sees the accepted socket closed when the connection is disposed.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

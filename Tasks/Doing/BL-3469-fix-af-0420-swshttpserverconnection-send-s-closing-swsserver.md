---
id: BL-3469
title: Fix AF-0420: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3469 — Fix AF-0420: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close

## Goal

The defect the audit office reported as AF-0420 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0420 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0420-swshttpserverconnection-send-s-closing-swsserverse.md`.

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'pendingSends.Add(new SwsServerSend([], serverBusyUntil, true));' -> 'false'. The third argument is isClose: the send queued after a closing reply (swsclose, close after the reply) is what ends the connection. With false, the stand-in server no longer closes after such a reply. No test checks that the connection ends.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600
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

---
id: BL-5075
title: Fix AF-0716: Surviving mutant: libcurl's own TCP socket can be opened without TCP_NODELAY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5075 — Fix AF-0716: Surviving mutant: libcurl's own TCP socket can be opened without TCP_NODELAY

## Goal

The defect the audit office reported as AF-0716 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0716 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0716-surviving-mutant-libcurl-s-own-tcp-socket-can-be-o.md`.

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339`

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339`

Mutant '{ NoDelay = true }' -> '{ NoDelay = false }' survived. curl sets TCP_NODELAY by default (--tcp-nodelay is on). No test reads the option back from the socket SocketCallbackConnector opens.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339:true -Member Open -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

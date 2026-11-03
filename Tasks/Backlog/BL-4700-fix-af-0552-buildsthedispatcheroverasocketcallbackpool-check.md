---
id: BL-4700
title: Fix AF-0552: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4700 — Fix AF-0552: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null

## Goal

The defect the audit office reported as AF-0552 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0552 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0552-buildsthedispatcheroverasocketcallbackpool-checks.md`.

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`

'Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));' is the only check of the dispatcher; the other assertions check CreateConnectOnlyConnector. A dispatcher built without the SocketCallbackConnector passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs -Pattern 'Assert.IsNotNull\(ManagedCurlEngine.CreateDispatcher'
```

- Expected: An assertion that the dispatcher's pool sits over a SocketCallbackConnector.
- Actual: ManagedEasyTransferTests.Sockets.cs:71: Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(...));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.

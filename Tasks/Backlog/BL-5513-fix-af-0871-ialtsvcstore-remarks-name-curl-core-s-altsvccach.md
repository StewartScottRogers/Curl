---
id: BL-5513
title: Fix AF-0871: IAltSvcStore remarks name Curl.Core's AltSvcCache as its implementation and Curl.Console as its setter; neither is true
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5513 — Fix AF-0871: IAltSvcStore remarks name Curl.Core's AltSvcCache as its implementation and Curl.Console as its setter; neither is true

## Goal

The defect the audit office reported as AF-0871 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0871 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs:8`

Location: `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs:8`

Remarks: 'Implemented in <c>Curl.Core.UnitLibrary</c> (<c>AltSvcCache</c>, ADR-0175) and set on HttpRequestOptions.AltSvcStore by <c>Curl.Console</c>'. Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs:27 declares 'public sealed class AltSvcCache(TimeProvider timeProvider, IDiagnosticLog? diagnosticLog = null)', which does not implement IAltSvcStore. The only implementation is Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:54 'internal sealed class AltSvcTransferCache : IAltSvcStore'. The store is set by Curl.Engine.UnitLibrary/TransferContextFactory.cs:391 ('http with { AltSvcStore = altSvc }'), not by Curl.Console. An agent changing alt-svc storage would edit the wrong type in the wrong project.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs -Pattern 'Implemented in <c>Curl.Core.UnitLibrary</c> \(<c>AltSvcCache</c>').Count; (Select-String -Path Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs -Pattern 'IAltSvcStore').Count; (Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'class AltSvcTransferCache : IAltSvcStore').Count
```

- Expected: 0, then any, then 1: the remarks name AltSvcTransferCache in Curl.Engine (or AltSvcCache implements the interface)
- Actual: 1, 0, 1: the remarks name AltSvcCache, which does not implement IAltSvcStore

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.

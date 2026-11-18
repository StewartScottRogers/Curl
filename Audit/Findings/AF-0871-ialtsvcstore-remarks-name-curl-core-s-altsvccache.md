---
id: AF-0871
title: IAltSvcStore remarks name Curl.Core's AltSvcCache as its implementation and Curl.Console as its setter; neither is true
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs:IAltSvcStore:false-doc-comment
reproduction: none
task: BL-5513
tasks: BL-5513
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0871 - IAltSvcStore remarks name Curl.Core's AltSvcCache as its implementation and Curl.Console as its setter; neither is true

## Summary

Medium finding from the truthfulness auditor at `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs:8`: IAltSvcStore remarks name Curl.Core's AltSvcCache as its implementation and Curl.Console as its setter; neither is true. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs:8`

Remarks: 'Implemented in <c>Curl.Core.UnitLibrary</c> (<c>AltSvcCache</c>, ADR-0175) and set on HttpRequestOptions.AltSvcStore by <c>Curl.Console</c>'. Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs:27 declares 'public sealed class AltSvcCache(TimeProvider timeProvider, IDiagnosticLog? diagnosticLog = null)', which does not implement IAltSvcStore. The only implementation is Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:54 'internal sealed class AltSvcTransferCache : IAltSvcStore'. The store is set by Curl.Engine.UnitLibrary/TransferContextFactory.cs:391 ('http with { AltSvcStore = altSvc }'), not by Curl.Console. An agent changing alt-svc storage would edit the wrong type in the wrong project.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs -Pattern 'Implemented in <c>Curl.Core.UnitLibrary</c> \(<c>AltSvcCache</c>').Count; (Select-String -Path Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs -Pattern 'IAltSvcStore').Count; (Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'class AltSvcTransferCache : IAltSvcStore').Count
```

- Expected: 0, then any, then 1: the remarks name AltSvcTransferCache in Curl.Engine (or AltSvcCache implements the interface)
- Actual: 1, 0, 1: the remarks name AltSvcCache, which does not implement IAltSvcStore

## Re-audits

## Log

- 2026-11-18: filed proposed.

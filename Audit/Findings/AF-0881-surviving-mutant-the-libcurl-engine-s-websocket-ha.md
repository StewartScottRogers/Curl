---
id: AF-0881
title: Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateProtocolHandlers-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:144:false
task: none
tasks:
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0881 - Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.cs:144`: Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws. Reappeared; previously AF-0717.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:144`

Mutant 'bool tracesWs = true,' (from 'false') survived in the Engine sample and in the targeted -Site run. ManagedCurlEngine.cs:241-262 calls EngineComposition.CreateProtocolHandlers without a tracesWs argument, so the libcurl engine relies on this default. Under the mutant every libcurl WebSocket transfer writes WS trace lines to the verbose and debug output. No Engine test pins the absence of WS trace lines on a libcurl WebSocket transfer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:144:false -Member CreateProtocolHandlers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 144)

## Re-audits

## Log

- 2026-11-19: filed proposed.

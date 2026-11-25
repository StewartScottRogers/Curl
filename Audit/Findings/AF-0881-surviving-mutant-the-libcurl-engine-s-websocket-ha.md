---
id: AF-0881
title: Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateProtocolHandlers-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:144:false
task: BL-5545
tasks: BL-5545
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0602.md
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

- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the -Site command: EngineComposition.cs:144 (resolvedLine 144), bool tracesWs = false -> true, outcome killed (2 baseline-failing AwsSigV4 tests excluded). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.

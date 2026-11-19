---
id: BL-5545
title: Fix AF-0881: Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5545 — Fix AF-0881: Surviving mutant: the libcurl engine's WebSocket handler can trace WS frames without --trace-config ws

## Goal

The defect the audit office reported as AF-0881 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0881 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:144`

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:144`

Mutant 'bool tracesWs = true,' (from 'false') survived in the Engine sample and in the targeted -Site run. ManagedCurlEngine.cs:241-262 calls EngineComposition.CreateProtocolHandlers without a tracesWs argument, so the libcurl engine relies on this default. Under the mutant every libcurl WebSocket transfer writes WS trace lines to the verbose and debug output. No Engine test pins the absence of WS trace lines on a libcurl WebSocket transfer.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/EngineComposition.cs line 144` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/EngineComposition.cs line 144`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test CreateProtocolHandlers_WsTransferWithoutTracesWsArgument_WritesNoWsTraceLines (EngineCompositionWsTraceTests): with the default flipped to true it fails (6 [WS] lines, expected 1); reverted, all 3103 Engine fast tests pass.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: efacbeda 9742157d.
- 2026-11-19: Doing -> Done. Mutant killed by new test

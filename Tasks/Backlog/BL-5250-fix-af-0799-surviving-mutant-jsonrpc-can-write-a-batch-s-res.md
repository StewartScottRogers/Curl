---
id: BL-5250
title: Fix AF-0799: Surviving mutant: --jsonrpc can write a batch's responses out of call order when the first one received answers an early call
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs]
requirement: none
created: 2026-11-15
completed:
---
# BL-5250 — Fix AF-0799: Surviving mutant: --jsonrpc can write a batch's responses out of call order when the first one received answers an early call

## Goal

The defect the audit office reported as AF-0799 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0799 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49`

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49`

The seed 770877 sample of the drawn library Curl.Extension.JsonRpc.UnitLibrary: 'if (index >= 0)' -> 'if (index > 0)' in Paired survived. With the mutant, a response found at index 0 is not written in its call's place. It stays in the list and is written later among the unanswered ones. Responses [id1, id3, id2] to calls 1, 2, 3 then print id2, id3, id1 instead of id1, id2, id3, so standard output differs. Every test puts the index-0 match on the last call (Pair_ABatchAnsweredOutOfOrder: [id3, err, id2, id1]; the hook test: [id2, id1]), where the unpaired tail happens to give the same order.

Reproduction: the audit office's mutation test changed `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs line 49` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs line 49`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.

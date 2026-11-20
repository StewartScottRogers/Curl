---
id: BL-5587
title: Fix AF-0899: Surviving mutant: the zstd Huffman weight check can accept trees libzstd refuses as corrupt
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5587 — Fix AF-0899: Surviving mutant: the zstd Huffman weight check can accept trees libzstd refuses as corrupt

## Goal

The defect the audit office reported as AF-0899 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0899 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:165`

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:165`

Mutant weights.Count(weight => weight == 1) >= 2 -> weight != 1 survived the whole of Curl.Zstandard.UnitTests. The check is libzstd's HUF_readStats rule (two weight-1 symbols). Mutated, a description with one explicit weight 2 (completed to [2,2], no weight-1 symbol) is accepted and decoded instead of refused as corruption_detected. A valid two-symbol tree [1,1] is refused instead. Either way a --compressed zstd response's output bytes and exit code change. No test feeds a tree that this check refuses.

Reproduction: the finding's reproduction runs an audit office tool, which a lane may not run; the audit office reruns it on the re-audit. Prove the fix with a test instead.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] A fast test fails on the code as the evidence above describes it and passes after the fix; Notes name the test.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: cce9bcd4 9742157d.

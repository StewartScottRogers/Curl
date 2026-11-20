---
id: BL-5588
title: Fix AF-0900: Surviving mutant: the Huffman table build can skip the top weight's rank start
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5588 — Fix AF-0900: Surviving mutant: the Huffman table build can skip the top weight's rank start

## Goal

The defect the audit office reported as AF-0900 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0900 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:180`

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:180`

Mutant for (var weight = 1; weight <= maxNumberOfBits; weight++) -> weight < maxNumberOfBits survived. rankStarts[maxNumberOfBits] then keeps its count, not its start. With one symbol at the top weight the two coincide. With a one-bit tree (one explicit weight 1, completed to [1,1], maxNumberOfBits 1), Fill starts at index 2 of a 2-entry table and throws instead of decoding the literals. No test decodes a Huffman tree whose codes are all one bit long.

Reproduction: the finding's reproduction runs an audit office tool, which a lane may not run; the audit office reruns it on the re-audit. Prove the fix with a test instead.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] A fast test fails on the code as the evidence above describes it and passes after the fix; Notes name the test.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: cce9bcd4 9742157d.

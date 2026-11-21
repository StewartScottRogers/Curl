---
id: BL-5699
title: Fix AF-0940: Surviving mutant: the Brotli UTF8 context of byte 0xC0 can be wrong, mis-decoding output
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5699 — Fix AF-0940: Surviving mutant: the Brotli UTF8 context of byte 0xC0 can be wrong, mis-decoding output

## Goal

The defect the audit office reported as AF-0940 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0940 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35`

Drawn library, seed 812510: (value >= 192 ? 2 : 0) mutated to value > 192 survived. Byte 0xC0 then gets context 0 instead of 2 (RFC 7932 section 7.1), so a UTF8-mode stream whose previous byte is 0xC0 picks the wrong literal tree and decodes wrong bytes. No test covers the 192 boundary.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs line 35` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs line 35`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by `BrotliLiteralContextTests.Of_Mode_GivesSection71Context` DataRows (2, 0xC0, 0, 2) and (2, 0xBF, 0, 1). Checked by hand: with `>` the 0xC0 row failed; reverted, all 191 library tests pass. Test-only change, production code untouched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4fbbaa0b 9742157d.
- 2026-11-21: Doing -> Done. Added 0xC0 and 0xBF literal-context test rows; hand mutant killed

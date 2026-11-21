---
id: BL-5700
title: Fix AF-0941: Surviving mutant: a 300 response can be stored as a compression dictionary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5700 — Fix AF-0941: Surviving mutant: a 300 response can be stored as a compression dictionary

## Goal

The defect the audit office reported as AF-0941 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0941 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147`

Drawn library, seed 812510: Report.ResponseCode: >= 200 and < 300 mutated to <= 300 survived; a 300 Multiple Choices response carrying Use-As-Dictionary would be stored and later advertised. No test sends a 300.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs line 147` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs line 147`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added 300 and 199 cases to `ExecuteAsync_AResponseThatIsNotAUsableDictionary_IsNotStored_Rfc9842Section2_1`; with `<= 300` applied by hand it fails, with it reverted all 194 pass. Killing test: that one. Production code unchanged; solution-wide build not rerun beyond this test project.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: dbd77435 9742157d.
- 2026-11-21: Doing -> Done. Added 300 and 199 cases; the <= 300 mutant now fails a test

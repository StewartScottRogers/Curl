---
id: BL-5697
title: Fix AF-0938: Surviving mutant: a Brotli repeat code that exactly fills its alphabet can be refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5697 — Fix AF-0938: Surviving mutant: a Brotli repeat code that exactly fills its alphabet can be refused

## Goal

The defect the audit office reported as AF-0938 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0938 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55`

Drawn library, seed 812510: if (added > alphabetSize - _symbol) mutated to >= survived. A repeat ending exactly at the alphabet's end is valid (RFC 7932 section 3.5); the mutant throws BrotliFormatException, so a valid dcb response fails with bad content encoding instead of writing its body.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs line 55` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs line 55`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by the new test `BrotliPrefixCodeReaderTests.Read_RepeatEndingExactlyAtAlphabetEnd_Reads` (a 256-symbol alphabet filled exactly by code 16 repeats of 5, 17, 65, 256). Verified by hand: with `>=` it fails, reverted it passes. Production code unchanged. Fast tests 190/190 green.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 39c5228c 9742157d.
- 2026-11-21: Doing -> Done. Mutant killed by new test

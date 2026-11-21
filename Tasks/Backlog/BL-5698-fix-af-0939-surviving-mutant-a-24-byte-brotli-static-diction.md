---
id: BL-5698
title: Fix AF-0939: Surviving mutant: a 24-byte Brotli static-dictionary word can be refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5698 — Fix AF-0939: Surviving mutant: a 24-byte Brotli static-dictionary word can be refused

## Goal

The defect the audit office reported as AF-0939 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0939 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57`

Drawn library, seed 812510: length > MaximumWordLength (24) mutated to >= survived. RFC 7932 section 8 allows copy lengths 4 to 24; the mutant refuses every 24-byte word and fails valid streams. No test decodes a 24-byte static word.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs line 57` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs line 57`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.

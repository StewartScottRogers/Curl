---
id: BL-5701
title: Fix AF-0942: Surviving mutant: a ':' with no name after it can be compiled as a URL-pattern group
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5701 — Fix AF-0942: Surviving mutant: a ':' with no name after it can be compiled as a URL-pattern group

## Goal

The defect the audit office reported as AF-0942 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0942 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82`

Drawn library, seed 812510: character == ':' && nameEnd > index + 1 mutated to >= survived. A bare ':' then becomes a wildcard segment instead of a literal colon, so a dictionary matches URLs it should not and its Available-Dictionary header is sent on them. No test has a match pattern with a bare colon.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs line 82` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs line 82`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.

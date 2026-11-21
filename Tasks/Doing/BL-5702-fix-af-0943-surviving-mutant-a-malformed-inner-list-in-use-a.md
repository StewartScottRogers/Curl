---
id: BL-5702
title: Fix AF-0943: Surviving mutant: a malformed inner list in Use-As-Dictionary can be accepted
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5702 — Fix AF-0943: Surviving mutant: a malformed inner list in Use-As-Dictionary can be accepted

## Goal

The defect the audit office reported as AF-0943 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0943 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138`

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138`

Drawn library, seed 812510: if (item is null || !IsInnerListItemEnd()) mutated to && survived; an inner-list item followed by a non-separator is no longer refused, so an invalid header (RFC 8941 section 4.2.1.2) can be stored as a dictionary. No test parses such a list.

Reproduction: the audit office's mutation test changed `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs line 138` as the evidence above describes (the mutant operator is `||`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs line 138`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The line-138 mutant needs items.Add(item!) to compile (nullable warnings are errors). Killed by Parse_InvalidStructuredField_ReturnsNull rows l=(1 2x) and l=(1"a") in UseAsDictionaryTests.cs; reverted, all pass. Test-only change, no production edit.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 85c3ce90 9742157d.

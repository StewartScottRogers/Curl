---
id: BL-5266
title: Fix AF-0812: Surviving mutant: URL encoding can leave a lower-case %xx escape un-upper-cased, or index before the string
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartText.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5266 — Fix AF-0812: Surviving mutant: URL encoding can leave a lower-case %xx escape un-upper-cased, or index before the string

## Goal

The defect the audit office reported as AF-0812 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0812 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:218`

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:218`

In IsLowerCaseEscape, `bytes[index + 1]` changed to `bytes[index - 1]` survived. AppendEncodedByte uses it to upper-case an escape as urlencode_str does. With the mutant, '%aF' keeps its lower-case digit unless the byte before '%' happens to be lower-case, and an escape at index 0 reads bytes[-1] and throws. The URL text the engine produces and sends changes, and no test pins an escape whose first hex digit alone is lower-case.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/UrlPartText.cs line 218` as the evidence above describes (the mutant operator is `+1`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/UrlPartText.cs line 218`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 000aa3d9 9742157d.

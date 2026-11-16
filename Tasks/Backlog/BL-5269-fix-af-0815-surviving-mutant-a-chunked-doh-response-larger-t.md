---
id: BL-5269
title: Fix AF-0815: Surviving mutant: a chunked DoH response larger than curl's buffer can be read on instead of refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohResponseReader.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5269 — Fix AF-0815: Surviving mutant: a chunked DoH response larger than curl's buffer can be read on instead of refused

## Goal

The defect the audit office reported as AF-0815 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0815 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:158`

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:158`

In ReadChunkAsync, `return false;` (the refusal when body.Count + size > MaximumBodyLength) changed to `return true;` survived. The reader then goes on without reading the chunk, treating its data as the next size line, instead of failing the DoH resolve as curl does for an oversize answer. No test sends a chunked DoH body past the limit.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/DohResponseReader.cs line 158` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/DohResponseReader.cs line 158`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.

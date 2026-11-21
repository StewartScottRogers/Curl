---
id: BL-5696
title: Fix AF-0937: Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5696 — Fix AF-0937: Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit

## Goal

The defect the audit office reported as AF-0937 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0937 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98`

Seed 812510 sample: if (elapsed <= MaxBlockedMilliseconds) mutated to < survived; a pong of exactly the limit then fails the port. No test pins the boundary.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs line 98` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs line 98`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.

---
id: BL-5273
title: Fix AF-0819: Surviving mutant: an ObliviousDoHConfigs list with bytes after its vector can be accepted
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5273 — Fix AF-0819: Surviving mutant: an ObliviousDoHConfigs list with bytes after its vector can be accepted

## Goal

The defect the audit office reported as AF-0819 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0819 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30`

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30`

`list.TryReadVector(out entries) && list.IsEmpty` changed to `||` survived. A configs body with trailing bytes after the length-prefixed list (malformed under RFC 9230 section 5) is then used instead of giving null. Config_SelectFrom_MalformedList_GivesNull has no row with trailing bytes after a valid list.

Reproduction: the audit office's mutation test changed `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs line 30` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs line 30`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.

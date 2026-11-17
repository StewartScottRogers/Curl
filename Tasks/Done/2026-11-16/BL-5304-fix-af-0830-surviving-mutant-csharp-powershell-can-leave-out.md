---
id: BL-5304
title: Fix AF-0830: Surviving mutant: --csharp/--powershell can leave out a -F form body of exactly the 1 MiB read-ahead limit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5304 — Fix AF-0830: Surviving mutant: --csharp/--powershell can leave out a -F form body of exactly the 1 MiB read-ahead limit

## Goal

The defect the audit office reported as AF-0830 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0830 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87`

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:87`

Mutation of a drawn library (seed 413266): 'stream.Length is not (> 0 and <= MaxReadAheadBytes)' mutated to '< MaxReadAheadBytes' survived. The only size test (CodeGenerationTextFormTests.cs:32) uses MaxReadAheadBytes + 1, so nothing pins that a body of exactly MaxReadAheadBytes (1024 * 1024) is still read. With the mutant such a form gives Body null, and the generated program sends no body.

Reproduction: the audit office's mutation test changed `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs line 87` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs line 87`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by `From_AFormOfExactlyTheReadAheadLimit_IsStillRead` (CodeGenerationTextFormTests.cs); verified by applying `<` by hand, test failed, reverted, all 47 pass. Test-only change, no production edit.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: da75c15a 9742157d.
- 2026-11-16: Doing -> Done. A form body of exactly the 1 MiB read-ahead limit is pinned by a new test that kills the mutant

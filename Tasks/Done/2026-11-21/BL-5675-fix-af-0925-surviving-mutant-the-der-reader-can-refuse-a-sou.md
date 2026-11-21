---
id: BL-5675
title: Fix AF-0925: Surviving mutant: the DER reader can refuse a source of exactly curl's maximum length
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/DerReader.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5675 — Fix AF-0925: Surviving mutant: the DER reader can refuse a source of exactly curl's maximum length

## Goal

The defect the audit office reported as AF-0925 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0925 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Output.UnitLibrary/DerReader.cs:77`

Location: `Curl.Output.UnitLibrary/DerReader.cs:77`

Mutant 'end - start > MaximumSourceLength' -> 'end - start >= MaximumSourceLength' survived (seed 276683 sample). MaximumSourceLength is 0x40000 (line 20). The mutant refuses to read a DER element spanning exactly that many bytes, which changes the certificate text the verbose and certinfo output prints. No test reads a source at the boundary.

Reproduction: the audit office's mutation test changed `Curl.Output.UnitLibrary/DerReader.cs line 77` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Output.UnitLibrary/DerReader.cs line 77`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `TryRead_SourceOfExactlyTheMaximumLength_IsRead` (mutant applied by hand: it failed; reverted: all 917 Output tests pass). `TryRead_SourceOneByteOverTheMaximumLength_IsRefused` pins the other side.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: ea97f8f0 9742157d.
- 2026-11-21: Doing -> Done. Boundary tests kill the mutant

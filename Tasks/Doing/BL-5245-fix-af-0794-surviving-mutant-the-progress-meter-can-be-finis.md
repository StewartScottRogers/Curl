---
id: BL-5245
title: Fix AF-0794: Surviving mutant: the progress meter can be finished with the transfer's success inverted
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5245 — Fix AF-0794: Surviving mutant: the progress meter can be finished with the transfer's success inverted

## Goal

The defect the audit office reported as AF-0794 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0794 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117`

Mutant 'progress?.FinishMeter(result?.IsSuccess == true)' -> '== false' survived (seed 5851). The meter would be told a successful transfer failed and a failed one succeeded, which decides the final progress-meter line the tool writes to stderr. No test pins FinishMeter's argument through FollowFinishingMeterAsync.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs line 117` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs line 117`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 66e745c2 9742157d.

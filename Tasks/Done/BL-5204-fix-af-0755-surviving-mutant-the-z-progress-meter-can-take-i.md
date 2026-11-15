---
id: BL-5204
title: Fix AF-0755: Surviving mutant: the -Z progress meter can take its speed window from an unset sample
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5204 — Fix AF-0755: Surviving mutant: the -Z progress meter can take its speed window from an unset sample

## Goal

The defect the audit office reported as AF-0755 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0755 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282`

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282`

Mutant 'speedSamplesWrapped |= nextSpeedSample == 0;' -> '!= 0' survived. After the first sample the meter would compute the speed from speedSamples[next], a default (0,0,0) entry, instead of from the run's start, so the Speed column of the parallel progress meter changes. No test pins the speed before ten samples.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs line 282` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs line 282`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ParallelProgressMeterTests.DrawIfDue_FewerThanTenSamplesAfterALateStart_MeasuresTheSpeedSinceTheRunStarted` (mutant gave speed 333, expected 2000; the clock starts at 10 s so the unset sample time 0 differs from the start). Reverted, all 3038 fast Engine tests pass. Test-only change, no production code touched.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: a013c758 9742157d.
- 2026-11-14: Doing -> Done. Mutant killed by new late-start speed test

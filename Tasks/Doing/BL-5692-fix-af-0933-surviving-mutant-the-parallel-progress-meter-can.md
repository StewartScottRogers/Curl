---
id: BL-5692
title: Fix AF-0933: Surviving mutant: the parallel progress meter can treat an unknown upload total as known
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ParallelProgressMeter.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5692 — Fix AF-0933: Surviving mutant: the parallel progress meter can treat an unknown upload total as known

## Goal

The defect the audit office reported as AF-0933 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0933 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235`

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235`

Seed 812510 sample of Curl.Engine.UnitLibrary: totals.UploadKnown && transfer.UploadTotal != 0 mutated to || survived. UploadKnown starts true, so the mutant marks the run's upload size known even when a transfer's total is 0, changing the -Z progress meter's upload share written to standard error. No test runs the parallel meter with an unknown upload size.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs line 235` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs line 235`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `DrawIfDue_OneUploadOfKnownSizeBesideOneOfUnknownSize_ShowsNoUploadPercent` (ParallelProgressMeterTests): with `||` at line 235 it fails (shows 50%), with `&&` all 3135 Engine fast tests pass. Test-only change, production code untouched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 55fc6ee5 9742157d.

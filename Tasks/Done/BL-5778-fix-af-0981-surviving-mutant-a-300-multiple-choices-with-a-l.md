---
id: BL-5778
title: Fix AF-0981: Surviving mutant: a 300 Multiple Choices with a Location can be treated as not a redirect (>= 300 -> > 300)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5778 — Fix AF-0981: Surviving mutant: a 300 Multiple Choices with a Location can be treated as not a redirect (>= 300 -> > 300)

## Goal

The defect the audit office reported as AF-0981 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0981 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111`

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111`

Invoke-MutationTest -Library Curl.Extension.OpenTelemetry.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: not null }' to '> 300 and < 400'. A followed 300 hop would export its own span and start a new trace instead of carrying the chain's trace id. The redirect tests use only 302 and 307.

Reproduction: the audit office's mutation test changed `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs line 111` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs line 111`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by OpenTelemetryRedirectTests.ExecuteAsync_A300WithALocationThenA200_ExportsOneSpanBecauseThe300HopIsFollowed_BL5778: with > 300 applied it fails; reverted, all 67 pass.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b83ebf3a 9742157d.
- 2026-11-22: Doing -> Done. Mutant killed by new 300 test

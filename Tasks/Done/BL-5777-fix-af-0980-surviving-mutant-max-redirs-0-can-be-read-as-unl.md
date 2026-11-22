---
id: BL-5777
title: Fix AF-0980: Surviving mutant: --max-redirs 0 can be read as unlimited when deciding whether a redirect is followed (< 0 -> <= 0)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5777 — Fix AF-0980: Surviving mutant: --max-redirs 0 can be read as unlimited when deciding whether a redirect is followed (< 0 -> <= 0)

## Goal

The defect the audit office reported as AF-0980 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0980 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108`

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108`

Invoke-MutationTest -Library Curl.Extension.OpenTelemetry.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'http.MaxRedirects < 0 || http.RedirectsFollowed < http.MaxRedirects' to 'http.MaxRedirects <= 0 || ...'. With -L --max-redirs 0, a 3xx would count as a followed redirect, so the hook would defer the span export to a hop curl never makes and drop the span. OpenTelemetryRedirectTests covers maxRedirects 1 and -1 but not 0.

Reproduction: the audit office's mutation test changed `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs line 108` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs line 108`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by ExecuteAsync_MaxRedirsZeroAndARedirect_ExportsTheSpanBecauseNoHopFollows (new, OpenTelemetryRedirectTests): with `<= 0` applied by hand it fails, reverted all 66 pass. Test-only change; no production edit.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 700f96ff 9742157d.
- 2026-11-22: Doing -> Done. New max-redirs 0 test kills the mutant; build and fast tests green

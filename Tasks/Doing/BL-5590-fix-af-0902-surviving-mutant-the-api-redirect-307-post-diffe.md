---
id: BL-5590
title: Fix AF-0902: Surviving mutant: the api-redirect-307-post differential case can stop following redirects
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5590 — Fix AF-0902: Surviving mutant: the api-redirect-307-post differential case can stop following redirects

## Goal

The defect the audit office reported as AF-0902 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0902 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:27`

Location: `Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:27`

Mutant FollowsRedirects = true -> false in new("api-redirect-307-post", "-sL -d x=1 {U}/r307", ...) survived Curl.Conformance.Differential.UnitTests. The case pairs curl -L with an API request. Mutated, the API side no longer follows the 307, so the differential compares two different transfers. No unit test pins the cases' request settings against their command lines.

Reproduction: the finding's reproduction runs an audit office tool, which a lane may not run; the audit office reruns it on the re-audit. Prove the fix with a test instead.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] A fast test fails on the code as the evidence above describes it and passes after the fix; Notes name the test.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Production code was already correct; the gap was a missing pin. Added `All_EveryCase_FollowsRedirectsExactlyWhenItsCommandPassesTheLocationOption` and `All_Redirect307PostCase_FollowsTheRedirectWithItsPostBody` in `Curl.Conformance.Differential.UnitTests/Api/ApiDifferentialCasesTests.cs`; mutating FollowsRedirects to false on api-redirect-307-post fails both. The test project was added outside `touches` (tests only). Fast tests: 623 pass.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: cce9bcd4 9742157d.

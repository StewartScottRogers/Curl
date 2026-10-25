---
id: BL-3516
title: Fix AF-0438: UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-25
completed:
---
# BL-3516 — Fix AF-0438: UpstreamTest1173Script.Section's InSynopsis = false on an EXAMPLE heading can become true with no test failing

## Goal

The defect the audit office reported as AF-0438 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0438 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0438-upstreamtest1173script-section-s-insynopsis-false.md`.

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215`

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215`

Sampled mutant (seed 0) survived: in the ExampleHeading branch, 'state.InSynopsis = false;' -> 'state.InSynopsis = true;'. The man-page checker then treats an EXAMPLE section as SYNOPSIS, so test1173's synopsis checks run on example text. That changes the problems the script reports and its exit code. No Curl.Conformance.UnitTests test gives it a page whose EXAMPLE section follows SYNOPSIS and would break a synopsis rule.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1173Script.cs:215:false -Member Section -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.

---
id: BL-3468
title: Fix AF-0419: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3468 — Fix AF-0419: UpstreamTest1544Script.Unfold's folded.Length > 0 can become >= 0 with no test failing, so every unfolded line gains a leading space

## Goal

The defect the audit office reported as AF-0419 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0419 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0419-upstreamtest1544script-unfold-s-folded-length-0-ca.md`.

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'line = folded.Length > 0 ? folded + " " + line : line;' -> '>= 0'. With no pending fold, the line becomes " " + line. Its first character is then never '#', so comment lines are no longer dropped, and the option patterns the script matches see a shifted line. The script decides test1544's verdict, and no test notices the change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs:124:> -Member Unfold -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

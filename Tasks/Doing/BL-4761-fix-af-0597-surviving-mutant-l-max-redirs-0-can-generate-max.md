---
id: BL-4761
title: Fix AF-0597: Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary, Curl.Extension.CodeGeneration.UnitTests/PowerShellScriptTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4761 — Fix AF-0597: Surviving mutant: -L --max-redirs 0 can generate -MaximumRedirection 2147483647 without a test failing

## Goal

The defect the audit office reported as AF-0597 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0597 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0597-surviving-mutant-l-max-redirs-0-can-generate-maxim.md`.

Location: `Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76`

Location: `Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76`

Drawn library. request.MaxRedirects < 0 -> <= 0 survived: with FollowRedirects and MaxRedirects 0 the generated script follows unlimited redirects instead of none, so --powershell writes different bytes. For_FollowingRedirects_SetsMaximumRedirection checks only 7 and -1.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/PowerShellScript.cs:76:< -Member MaximumRedirection -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Curl.Extension.CodeGeneration.UnitTests/PowerShellScriptTests.cs to touches: the test that kills the mutant belongs there, and no task in Doing on origin/work/dark-factory names it.
- Fix: For_FollowingRedirects_SetsMaximumRedirection now also pins `-L --max-redirs 0` (FollowRedirects true, MaxRedirects 0) to ` -MaximumRedirection 0 `, which the `<= 0` mutant would write as 2147483647. No production change. A lane may not run the audit office's mutation tool, so the reproduction was checked by applying the `<= 0` mutation by hand: the test failed under it and passes without it.
- Resumed run: the earlier run was cut off with the hand-applied `<= 0` mutant still in PowerShellScript.cs; it was reverted, so production code is unchanged. dotnet build clean, fast tests green (0 failed). No production change, so no Measure-CodeQuality run was needed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

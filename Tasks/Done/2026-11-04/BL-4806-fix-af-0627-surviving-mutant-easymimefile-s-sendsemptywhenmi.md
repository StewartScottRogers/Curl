---
id: BL-4806
title: Fix AF-0627: Surviving mutant: EasyMimeFile's SendsEmptyWhenMissing can default to true, so a missing -F file part is sent empty, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests/Options/Http/EasyMimeFileTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4806 — Fix AF-0627: Surviving mutant: EasyMimeFile's SendsEmptyWhenMissing can default to true, so a missing -F file part is sent empty, and no test fails

## Goal

The defect the audit office reported as AF-0627 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0627 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0627-surviving-mutant-easymimefile-s-sendsemptywhenmiss.md`.

Location: `Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12`

Location: `Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12`

Invoke-MutationTest -Library Curl.Api.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'public sealed record EasyMimeFile(string Path, bool SendsEmptyWhenMissing = false)' -> '= true'. Curl.Console/EasyOptionsFromCommandLine.cs:586 (new EasyMimeFile(part.Content)) and Curl.Engine.UnitLibrary/FormPost.cs:184 (new EasyMimeFile(file)) rely on the default. With true, curl -F f=@missing and curl_formadd's file part send an empty part instead of failing with exit 26. No Curl.Api test pins the default.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12:false -Member EasyMimeFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: new `Curl.Api.UnitTests/Options/Http/EasyMimeFileTests.cs` pins the default (`new EasyMimeFile(path)` has `SendsEmptyWhenMissing` false) and the explicit true. Touches widened to that test file: the library's tests live in Curl.Api.UnitTests, and no task in Doing on work/dark-factory named it.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand: with the default flipped to `= true`, `New_EasyMimeFileWithOnlyAPath_FailsTheTransferWhenTheFileIsMissing` fails (killed); reverted. The re-audit runs the finding's own reproduction.
- No production code changed, so no Measure-CodeQuality run. `dotnet build` clean; fast tests: 115 test projects passed, none failed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A test pins EasyMimeFile's SendsEmptyWhenMissing default to false, so the AF-0627 mutant is killed

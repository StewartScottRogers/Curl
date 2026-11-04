---
id: BL-4841
title: Fix AF-0659: Surviving mutant: lib670 can go on after curl_formadd refuses its field
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib666To695Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4841 — Fix AF-0659: Surviving mutant: lib670 can go on after curl_formadd refuses its field

## Goal

The defect the audit office reported as AF-0659 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0659 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0659-surviving-mutant-lib670-can-go-on-after-curl-forma.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`

Mutant 'return false;' -> 'return true;' after a refused curl_formadd survived. The port then performs with a broken form instead of stopping, which changes its output. No test fails.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129:false -Member BindForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right; the mutant survived because the refused-formadd test checked only stderr, and on the multi case (672) the fake engine leaves no trace of a transfer. Added `Lib670_RefusedFormAddOnTheEasyInterface_PerformsNothing` (case 673, a counting `Performing` callback that would fail with SendError): it asserts no perform, exit 0 (upstream's `goto test_cleanup` with `result` still CURLE_OK) and the `curl_formadd() = 1` line.
- Added `Curl.Conformance.LibTest.UnitTests/Lib666To695Tests.cs` to `touches`: the fix is a test, and no task in Doing on origin/work/dark-factory names that project.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by hand: with Lib670.cs:129 changed to `return true;` the new test fails (`Assert.AreEqual(0, performs)`), i.e. the mutant is killed; the line was then restored. The re-audit confirms it with the audit tool.
- No production code changed, so no Measure-CodeQuality.ps1 run was needed; the test is platform-neutral (no paths, no OS text).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A test now kills the lib670 refused-curl_formadd mutant: a refused form performs nothing

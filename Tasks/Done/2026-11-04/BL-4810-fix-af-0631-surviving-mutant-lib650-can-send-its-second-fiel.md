---
id: BL-4810
title: Fix AF-0631: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib643To661Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4810 — Fix AF-0631: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails

## Goal

The defect the audit office reported as AF-0631 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0631 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0631-surviving-mutant-lib650-can-send-its-second-field.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'new(CurlFormOption.NameLength, (long)TestName.Length - 1)' -> '+ 1'. Upstream test650 sends the second part under the name cut by one byte (CURLFORM_NAMELENGTH). The mutant changes the multipart request bytes the port builds, and the LibTest tests never compare that part's name.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96:-1 -Member BuildForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests/Lib643To661Tests.cs` to touches: the fix is a test, and that file holds Lib650's test. No task in Doing on origin/work/dark-factory names it (BL-4806: Curl.Api.UnitLibrary, BL-4807: Curl.Extension.ResumableUpload.UnitLibrary).
- Fix: `Lib650_AddsTheSixFieldsAndPostsOnlyWhenAllSucceed` now checks the second `curl_formadd` call's `PointerName` ("fieldname") and its `NameLength` (8, the name cut by one byte, as upstream test650 sends it). No production code changed.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`- 1` -> `+ 1` at Lib650.cs:96). The Lib650 tests failed on `Assert.AreEqual(8L, form.Calls[1][1].Value)`, so the mutant is killed. The mutation was then reverted. The quality auditor's re-audit closes AF-0631.
- No quality run: only test code changed.
- Build clean and fast tests green on Windows. The test runs no file system, path or OS operation, so it does not depend on the platform.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Lib650's test now checks the second field's PointerName and NameLength 8, killing the NameLength +1 mutant

---
id: BL-4764
title: Fix AF-0600: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib751To1398Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4764 — Fix AF-0600: Surviving mutant: lib1396 can abort with 'returned NULL!' on an empty input without a test failing

## Goal

The defect the audit office reported as AF-0600 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0600 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0600-surviving-mutant-lib1396-can-abort-with-returned-n.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134`

taken < 0 -> taken <= 0 survived: a row whose escaped length is 0 then gives null and the program aborts, changing the lib1396 output the upstream case compares.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1396.cs:134:< -Member Escape -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The mutant survived because no escape row had an escaped length of 0. Added `Lib1396_EscapesAnEmptyInputToAnEmptyAnswerWithoutAborting` (`Curl.Conformance.LibTest.UnitTests/Lib751To1398Tests.cs`): an empty input with length 0 (strlen) must print `curl_easy_escape test 0 DONE ()` with no failures, as upstream curl_easy_escape("", 0) returns "". Lib1396.cs itself was already correct and did not change.
- Touches widened to that test file: no other task in Doing on origin/work/dark-factory names it.
- A lane may not run the audit office mutation tool, so I applied the mutation by hand (`taken < 0` -> `taken <= 0` at Lib1396.cs:134). The new test failed against it and passed once the mutation was reverted. This stands in for the reproduction; the re-audit confirms it.
- `dotnet build` is clean and the fast tests are green. Production code did not change, so no quality run was needed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. lib1396 escaping an empty input is pinned by a unit test that kills the taken <= 0 mutant

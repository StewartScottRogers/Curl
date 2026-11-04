---
id: BL-4792
title: Fix AF-0619: Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib2308To2504Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4792 — Fix AF-0619: Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing

## Goal

The defect the audit office reported as AF-0619 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0619 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0619-surviving-mutant-lib2405-s-port-can-fail-a-waitfds.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191`

Mutant 'Failed(..., count > 1, "curl_multi_waitfds() returned > 1 count of waitfds: ...")' -> 'count >= 1' survived. lib2405.c allows one descriptor for an added handle. The mutant fails the program, with a different exit code and stderr line, whenever waitfds answers 1. The tests only drive counts of 0 and above 1.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191:> -Member AddedHandleCheckPasses -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test only: `Lib2405_WhenTheAddedHandleGivesOneDescriptor_Passes` drives a waitfds count of exactly 1 for the added handle and expects exit 0 with empty stderr. Checked by hand that the mutant `count >= 1` at Lib2405.cs:191 fails this test (1 failed, 29 passed over the Lib2405 filter), then reverted it, since a lane may not run the mutation tool. No production code changed, so no quality run was needed. Build is clean and Curl.Conformance.LibTest.UnitTests passes its fast tests (1195 passed); the change is platform-neutral.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Lib2405 test drives a waitfds count of exactly 1 and kills the count >= 1 mutant

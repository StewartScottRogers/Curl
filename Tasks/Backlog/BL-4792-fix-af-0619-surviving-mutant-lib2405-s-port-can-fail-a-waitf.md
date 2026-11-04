---
id: BL-4792
title: Fix AF-0619: Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.

---
id: BL-2762
title: Fix AF-0251: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2762 — Fix AF-0251: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing

## Goal

The defect the audit office reported as AF-0251 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0251 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0251-the-netrc-host-not-found-info-line-s-condition-can.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`

Engine sample, seed 0: survived. Original `if (failure is null && result.Outcome != NetrcLookupOutcome.Found)`, mutated `==`. The mutant writes HostNotFoundLine when the host was found and leaves it out when it was not, so the -v standard-error bytes change in both cases. No test pins the line on the engine path.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142:!= -Member TryLookUpInNetrcFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix: two tests in `Curl.Engine.UnitTests/EasyAuthenticationOptionsReadingTests.cs` pin the
  `reportInfo` line of `TransferCredentialLookup.TryLookUp` on the engine path - a host found in the file
  reports no line, a host not in it reports `Could not find host other in the my.netrc file; using defaults`.
  No production code changed, so no quality run was needed.
- Added `Curl.Engine.UnitTests` to `touches`: the test belongs beside the library's other lookup tests, and
  no task in Doing on `origin/work/dark-factory` names it (BL-2750, BL-2751, BL-2755 checked).
- Reproduction: the audit guard refuses a lane `Audit/Tools/Invoke-MutationTest.ps1`, so the same mutant
  was applied by hand (line 142 `!=` to `==`) and `EasyAuthenticationOptionsReadingTests` run against it:
  both new tests failed (killed: 2 failed, 9 passed); the line was then restored. The quality auditor's
  re-audit runs the tool itself.
- Fast tests: every test project passed, 0 failures (Curl.Engine.UnitTests 1956 passed, 3 skipped).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. Two engine tests pin the netrc host-not-found -v line, killing the AF-0251 mutant

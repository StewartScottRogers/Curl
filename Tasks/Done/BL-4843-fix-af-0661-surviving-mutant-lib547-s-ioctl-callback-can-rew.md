---
id: BL-4843
title: Fix AF-0661: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4843 — Fix AF-0661: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead

## Goal

The defect the audit office reported as AF-0661 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0661 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0661-surviving-mutant-lib547-s-ioctl-callback-can-rewin.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`

Mutant 'command == IoctlCommand.RestartRead' -> '!=' survived. REWIND! would be written and the upload counter reset on the wrong commands, and not on a real restart. That changes stderr and the bytes uploaded. No test fails.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103:== -Member Ioctl -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: BlurbReader_ReadsOnceUntilRewound called Ioctl(RestartRead) and then Ioctl((IoctlCommand)2) back to back, so the `!=` mutant wrote the same REWIND! one call later and reset the counter all the same; the test could not tell them apart. Added BlurbReader_IoctlRestartReadAlone_RewindsTheUpload and BlurbReader_IoctlNop_NeitherRewindsNorSaysSo to Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs. Production code is unchanged, so no quality run was needed.
- Widened touches to that test file because the fix is a test. No other task in Doing on origin/work/dark-factory names it.
- Reproduction: a lane may not run the audit office's mutation tool, so I applied the mutant by hand (`==` -> `!=` at Lib547.cs:103) and ran Lib547Tests. 2 of 6 failed, so the mutant is killed, and the source was restored. dotnet build is clean. The fast tests exited 0: 115 projects passed and none failed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. lib547's ioctl callback is pinned to rewind only on RestartRead; the != mutant is killed

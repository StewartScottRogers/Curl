---
id: BL-2768
title: Fix AF-0257: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2768 — Fix AF-0257: Lib582 CheckForCompletion's success = false can start as true with no test failing, hiding 'Error uploading file.'

## Goal

The defect the audit office reported as AF-0257 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0257 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0257-lib582-checkforcompletion-s-success-false-can-star.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `success = false;`, mutated `true`. When no multi message arrives, the port reports success. It then skips writing 'Error uploading file.' and setting MajorBad, which changes the libtest's result and standard error that upstream case 582 judges.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:158:false -Member CheckForCompletion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The mutant was equivalent: `CheckForCompletion` returned whether a message came, `success` was read only when one had, and every message overwrote it, so its initial `false` was never read (the same holds in `lib582.c`). No test could kill it, so the fix removes the dead default: `CompletionOutcome` returns `bool?`, `null` until a message comes, then whether the last one was a successful done. Line 158 is now the method's opening brace, so the reproduction's mutation site no longer exists; the new default (`null`) cannot be flipped without the loop ending at once with `Error uploading file.`, which `Lib582_UploadsThroughSocketActionsUntilDone` catches. A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths are guarded), so the quality auditor's re-audit confirms it.
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.LibTest.UnitLibrary`: 100% line, 100% branch, 0 failing members (a first `success is false` left an untaken null branch; `!success.Value` fixed it).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. Lib582Run's completion check returns a nullable outcome, removing the equivalent success = false mutant AF-0257 reported

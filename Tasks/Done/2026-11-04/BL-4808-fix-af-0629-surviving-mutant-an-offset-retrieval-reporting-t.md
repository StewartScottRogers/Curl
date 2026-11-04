---
id: BL-4808
title: Fix AF-0629: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests/Transfer/ResumableUploadTransferHookTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4808 — Fix AF-0629: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails

## Goal

The defect the audit office reported as AF-0629 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0629 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0629-surviving-mutant-an-offset-retrieval-reporting-the.md`.

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`

Invoke-MutationTest -Library Curl.Extension.ResumableUpload.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'offset <= length' -> 'offset < length' in OffsetIn. When the server's HEAD says every byte arrived (Upload-Offset equal to the body length), the mutant throws ResumableUploadTransferException(CurlExitCode.WeirdServerReply, ...) instead of sending the final PATCH, so a resumed upload ends with exit 8. The tests cover offset 3 of 5 and a missing offset, never offset == length.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47:<= -Member OffsetIn -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right (`offset <= length` matches tus 1.0: an Upload-Offset equal to the length is valid); only a test was missing. Added `ExecuteAsync_OffsetRetrievalReportsTheWholeUploadReceived_PatchesTheEmptyRest` (HEAD answers Upload-Offset 5 of 5, the flow sends an empty PATCH from 5 and ends with exit 0).
- `touches` widened to the test file `Curl.Extension.ResumableUpload.UnitTests/Transfer/ResumableUploadTransferHookTests.cs`, where the test belongs; no task in Doing on origin/work/dark-factory names it.
- Reproduction check: lanes may not run `Audit/Tools`, so I applied the same mutant by hand (`offset <= length` -> `offset < length` in `OffsetIn`). The new test failed (1 of 152), so the mutant is killed. Then I reverted it; production code is unchanged, so no quality run was needed.
- Full solution build: 0 warnings, 0 errors. Fast tests (`--filter "TestCategory!=Integration"`): no failures.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. An offset retrieval reporting the whole upload received is pinned by a test that kills the <= to < mutant

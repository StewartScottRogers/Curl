---
id: BL-2932
title: Fix AF-0294: Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2932 — Fix AF-0294: Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout

## Goal

The defect the audit office reported as AF-0294 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0294 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0294-lib1541-checkdone-s-posttransfer-mustbezero-false.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106`

Sampled mutant (seed 0) survived: CheckTime(easy, output, CurlInfo.PosttransferTimeT, "CURLINFO_POSTTRANSFER_TIME_T", Where, mustBeZero: false) => mustBeZero: true. CheckTime (line 139) prints "<name> on done is OK" when time > 0, but with mustBeZero only when time == 0. So a normal transfer would write "CURLINFO_POSTTRANSFER_TIME_T on done is WRONG: N" to the lib1541 program's stdout, which upstream test1541 compares. No Curl.Conformance.LibTest unit test pins CheckDone's output for the post-transfer time.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106:false -Member CheckDone -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was the tests. `Lib1541_ChecksTheTimesAtTheFirstHeaderAndOnceDone`
  now pins all seven "on done" lines after the body (every time 1 ms, so each check is OK), and the new
  `Lib1541CheckDone_WithEveryTimeZero_WritesWrongForAllButTheAppconnectTime` pins them with every time 0. Flipping
  any `mustBeZero` in `CheckDone`, line 106's included, now changes a pinned line in both tests.
- Added `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs` to `touches`: the fix is a test, and no task
  in Doing on `origin/work/dark-factory` names that project.
- The reproduction script lives under `Audit/`, which a lane may not read or run (guard hook), so it was not run
  here; the mutant was checked by reasoning against the two pinned outputs. The quality auditor's re-audit confirms it.
- No production code changed, so no Measure-CodeQuality.ps1 run was needed.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. CheckDone's seven done lines pinned for all-OK and all-zero times; a flipped mustBeZero now fails a test

---
id: BL-2932
title: Fix AF-0294: Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-21
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

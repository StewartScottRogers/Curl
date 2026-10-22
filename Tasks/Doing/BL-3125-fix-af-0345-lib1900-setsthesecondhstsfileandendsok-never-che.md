---
id: BL-3125
title: Fix AF-0345: Lib1900_SetsTheSecondHstsFileAndEndsOk never checks that any HSTS file is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-22
completed:
---
# BL-3125 — Fix AF-0345: Lib1900_SetsTheSecondHstsFileAndEndsOk never checks that any HSTS file is set

## Goal

The defect the audit office reported as AF-0345 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0345 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0345-lib1900-setsthesecondhstsfileandendsok-never-check.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:254`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:254`

The body runs Lib1900 over a FakeCurlEngine and asserts only result.ExitCode == 0 and an empty standard error. Lib1900.TestAsync sets CURLOPT_HSTS twice (first-hsts.txt, then second-hsts.txt) and duplicates the handle, but the test never reads which HSTS file the handle (or its copy) holds. Dropping the second CURLOPT_HSTS, or keeping the first, would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs -Pattern 'Lib1900_SetsTheSecondHstsFileAndEndsOk' -Context 0,10
```

- Expected: The body asserts the handle's Hsts option is second-hsts.txt.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.AreEqual(string.Empty, run.Error.ToString()).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

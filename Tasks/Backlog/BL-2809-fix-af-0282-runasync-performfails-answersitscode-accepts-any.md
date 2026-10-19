---
id: BL-2809
title: Fix AF-0282: RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2809 — Fix AF-0282: RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names

## Goal

The defect the audit office reported as AF-0282 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0282 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0282-runasync-performfails-answersitscode-accepts-any-n.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:103`

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:103`

With curl_multi_perform scripted to answer CurlMultiCode.BadHandle, the test checks only Assert.AreNotEqual(0, result.ExitCode). The name promises 'its code', but any failure code passes, MajorBad from a wrong branch included. Lib540 is the same pattern as AF-0247.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'Assert.AreNotEqual\(0, result.ExitCode\)'
```

- Expected: The test asserts the exact exit code lib540 answers for a failed multi perform.
- Actual: Lib539To553Tests.cs:103 (and :143) assert only AreNotEqual(0, result.ExitCode).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.

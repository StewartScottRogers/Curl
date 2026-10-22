---
id: BL-3126
title: Fix AF-0346: Lib1582_PerformsWithNegotiateAndAnswersItsCode never checks that Negotiate is asked for
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3126 — Fix AF-0346: Lib1582_PerformsWithNegotiateAndAnswersItsCode never checks that Negotiate is asked for

## Goal

The defect the audit office reported as AF-0346 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0346 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0346-lib1582-performswithnegotiateandanswersitscode-nev.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:392`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:392`

The body only asserts that result.ExitCode is (int)CurlCode.LoginDenied, the code the fake engine was told to answer. Lib1582 sets CURLOPT_HTTPAUTH Negotiate and CURLOPT_USERPWD ':', but the test never reads the performed options (FakeCurlEngine's Performing callback could give them). Dropping or changing the Httpauth line would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1582_PerformsWithNegotiateAndAnswersItsCode' -Context 0,8
```

- Expected: The body asserts the performed options' HttpAuthentication is Negotiate.
- Actual: Only Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Lib1582 test now captures the performed EasyAuthenticationOptions through FakeCurlEngine.Performing and asserts HttpAuthentication is Negotiate and UserPassword is ":" before the exit code, so the asserts fall inside the reproduction's 8 lines of context. Test-only change: no production code, so no Measure-CodeQuality run. dotnet build clean (0 warnings) and the fast tests green on Windows; the change is platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. Lib1582 test asserts the performed options ask for Negotiate with credentials ':'

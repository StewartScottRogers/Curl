---
id: AF-0346
title: Lib1582_PerformsWithNegotiateAndAnswersItsCode never checks that Negotiate is asked for
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:Lib1582_PerformsWithNegotiateAndAnswersItsCode:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0346 - Lib1582_PerformsWithNegotiateAndAnswersItsCode never checks that Negotiate is asked for

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:392`: Lib1582_PerformsWithNegotiateAndAnswersItsCode never checks that Negotiate is asked for.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:392`

The body only asserts that result.ExitCode is (int)CurlCode.LoginDenied, the code the fake engine was told to answer. Lib1582 sets CURLOPT_HTTPAUTH Negotiate and CURLOPT_USERPWD ':', but the test never reads the performed options (FakeCurlEngine's Performing callback could give them). Dropping or changing the Httpauth line would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1582_PerformsWithNegotiateAndAnswersItsCode' -Context 0,8
```

- Expected: The body asserts the performed options' HttpAuthentication is Negotiate.
- Actual: Only Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode).

## Re-audits

## Log

- 2026-10-22: filed proposed.

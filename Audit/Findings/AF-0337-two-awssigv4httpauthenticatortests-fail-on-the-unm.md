---
id: AF-0337
title: Two AwsSigV4HttpAuthenticatorTests fail on the unmutated tree: Sign writes the secret key into the -v 'Server auth using AWS_SIGV4' line
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:Sign:baseline-failure
reproduction: none
task: BL-3117
tasks: BL-3117
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0337 - Two AwsSigV4HttpAuthenticatorTests fail on the unmutated tree: Sign writes the secret key into the -v 'Server auth using AWS_SIGV4' line

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`: Two AwsSigV4HttpAuthenticatorTests fail on the unmutated tree: Sign writes the secret key into the -v 'Server auth using AWS_SIGV4' line.

## Evidence

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`

Invoke-MutationTest.ps1 had to exclude two Curl.Engine.UnitTests tests from its baseline run with -ExcludeBaselineFailures: AwsSigV4HttpAuthenticatorTests.CreateAuthorization_CustomAuthorizationHeader_SendsNoValueButReportsTheScheme and CreateAuthorizationAsync_AwsSigV4BeforeAnyChallenge_SignsAndReportsWhatCurlPrints. Running them gives 'CollectionAssert.AreEqual failed. Different number of elements.' (Failed: 2, Passed: 10). The tests expect the info line "Server auth using AWS_SIGV4 with user 'AKID'", but line 118 reports $"Server auth using AWS_SIGV4 with user '{credential.UserName}' password '{credential.Password}'". That puts the AWS secret key into curl -v output, and the fast test run, and so CI, is red.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter "FullyQualifiedName~AwsSigV4HttpAuthenticatorTests"
```

- Expected: Passed! - all 12 AwsSigV4HttpAuthenticatorTests pass; no password in the reported line.
- Actual: Failed! - Failed: 2, Passed: 10 (CollectionAssert.AreEqual failed in both named tests).

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.

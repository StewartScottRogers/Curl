---
id: BL-3117
title: Fix AF-0337: Two AwsSigV4HttpAuthenticatorTests fail on the unmutated tree: Sign writes the secret key into the -v 'Server auth using AWS_SIGV4' line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3117 — Fix AF-0337: Two AwsSigV4HttpAuthenticatorTests fail on the unmutated tree: Sign writes the secret key into the -v 'Server auth using AWS_SIGV4' line

## Goal

The defect the audit office reported as AF-0337 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0337 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0337-two-awssigv4httpauthenticatortests-fail-on-the-unm.md`.

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`

Invoke-MutationTest.ps1 had to exclude two Curl.Engine.UnitTests tests from its baseline run with -ExcludeBaselineFailures: AwsSigV4HttpAuthenticatorTests.CreateAuthorization_CustomAuthorizationHeader_SendsNoValueButReportsTheScheme and CreateAuthorizationAsync_AwsSigV4BeforeAnyChallenge_SignsAndReportsWhatCurlPrints. Running them gives 'CollectionAssert.AreEqual failed. Different number of elements.' (Failed: 2, Passed: 10). The tests expect the info line "Server auth using AWS_SIGV4 with user 'AKID'", but line 118 reports $"Server auth using AWS_SIGV4 with user '{credential.UserName}' password '{credential.Password}'". That puts the AWS secret key into curl -v output, and the fast test run, and so CI, is red.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter "FullyQualifiedName~AwsSigV4HttpAuthenticatorTests"
```

- Expected: Passed! - all 12 AwsSigV4HttpAuthenticatorTests pass; no password in the reported line.
- Actual: Failed! - Failed: 2, Passed: 10 (CollectionAssert.AreEqual failed in both named tests).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.

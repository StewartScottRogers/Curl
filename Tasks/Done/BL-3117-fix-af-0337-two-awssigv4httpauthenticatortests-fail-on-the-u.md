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
completed: 2026-10-22
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- No code change: `AwsSigV4HttpAuthenticator.cs:118` already reports only `with user '{credential.UserName}'`, and `git log -S"password '{credential.Password}'"` finds no commit that ever wrote the password, so the line AF-0337 saw was most likely a planted defect left in the audit worktree (the seeder's catalogue), not a defect in this tree.
- The reproduction (`dotnet test Curl.Engine.UnitTests --filter "FullyQualifiedName~AwsSigV4HttpAuthenticatorTests"`) gives Passed: 12, Failed: 0. `dotnet build` is clean and the fast tests are green. No production code changed, so no Measure-CodeQuality.ps1 run was needed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. Reproduction passes (12/12) on this tree; the secret-key line was never committed here, so no code change was needed

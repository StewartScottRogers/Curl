---
id: BL-2120
title: Fix AF-0174: 21 Curl.Protocol.Http.UnitTests fail on the audited commit: HttpAuthUsingLines.AuthUsing writes the user's password into the verbose 'auth using' line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2120 — Fix AF-0174: 21 Curl.Protocol.Http.UnitTests fail on the audited commit: HttpAuthUsingLines.AuthUsing writes the user's password into the verbose 'auth using' line

## Goal

The defect the audit office reported as AF-0174 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0174 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0174-21-curl-protocol-http-unittests-fail-on-the-audite.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

The unmutated baseline of Curl.Protocol.Http.UnitTests failed (Invoke-MutationTest.ps1 stopped with 'the unmutated tests failed or timed out (exit 1)'); rerun with -ExcludeBaselineFailures, it named 21 failing tests in excludedTests, among them HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser, HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest, ExecuteAsync_DigestVerbose_WritesServerAuthUsingDigestBeforeBothRequests, ExecuteAsync_NtlmVerbose_WritesServerAuthUsingNtlmBeforeBothRequests and ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407. Line 28 builds the line as $"{(request.IsProxy ? "Proxy" : "Server")} auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'"; the tests expect curl's 'Server auth using Basic with user 'u'' with no password. With -v, Curl would print the -u password in clear to stderr. The tests catch it, but they are red, so the suite is not green on this commit.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs -Pattern "password '"
```

- Expected: No match: the line names the scheme and the user only, and dotnet test Curl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HttpAuthUsingLinesTests" passes.
- Actual: HttpAuthUsingLines.cs:28: ... auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'" (and 21 tests fail)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-13 (Claude): AF-0174 is most likely the audit's own planted defect, not a real one.
  Audit 20261012-165355's manifest (`Z:\repos\Curl.audit\20261012-165355\manifest.json`)
  lists PD-102 at `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`. It made the
  verbose "auth using" line append the password, which the 21 failing tests catch. CI was
  green on the audited commit 4e6ddbc5b (run 38324482077). Check that the real tree's
  `HttpAuthUsingLines.AuthUsing` writes no password and that those 21 tests pass. If both
  hold, there is nothing to fix: record that in the Log and finish the task.

## Log

- 2026-10-13: Created.

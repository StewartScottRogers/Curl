---
id: AF-0174
title: 21 Curl.Protocol.Http.UnitTests fail on the audited commit: HttpAuthUsingLines.AuthUsing writes the user's password into the verbose 'auth using' line
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:AuthUsing:failing-test
reproduction: none
task: BL-2120
tasks: BL-2120
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0174 - 21 Curl.Protocol.Http.UnitTests fail on the audited commit: HttpAuthUsingLines.AuthUsing writes the user's password into the verbose 'auth using' line

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`: 21 Curl.Protocol.Http.UnitTests fail on the audited commit: HttpAuthUsingLines.AuthUsing writes the user's password into the verbose 'auth using' line. Reported by an auditor flagged unreliable in 2026-10-12_1653.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

The unmutated baseline of Curl.Protocol.Http.UnitTests failed (Invoke-MutationTest.ps1 stopped with 'the unmutated tests failed or timed out (exit 1)'); rerun with -ExcludeBaselineFailures, it named 21 failing tests in excludedTests, among them HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser, HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest, ExecuteAsync_DigestVerbose_WritesServerAuthUsingDigestBeforeBothRequests, ExecuteAsync_NtlmVerbose_WritesServerAuthUsingNtlmBeforeBothRequests and ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407. Line 28 builds the line as $"{(request.IsProxy ? "Proxy" : "Server")} auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'"; the tests expect curl's 'Server auth using Basic with user 'u'' with no password. With -v, Curl would print the -u password in clear to stderr. The tests catch it, but they are red, so the suite is not green on this commit.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs -Pattern "password '"
```

- Expected: No match: the line names the scheme and the user only, and dotnet test Curl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HttpAuthUsingLinesTests" passes.
- Actual: HttpAuthUsingLines.cs:28: ... auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'" (and 21 tests fail)

## Re-audits

- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction: Select-String -Path Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs -Pattern "password '" gave no match. AuthUsing now formats only "... auth using {scheme} with user '{UserName}'". The unmutated Curl.Protocol.Http.UnitTests baseline in every mutation run passed with no tests excluded.

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.

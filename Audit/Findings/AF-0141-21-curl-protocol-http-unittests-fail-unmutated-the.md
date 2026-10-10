---
id: AF-0141
title: 21 Curl.Protocol.Http.UnitTests fail unmutated: the verbose 'auth using' line appends the user's password
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:AuthUsing:baseline-failure
reproduction: none
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0141 - 21 Curl.Protocol.Http.UnitTests fail unmutated: the verbose 'auth using' line appends the user's password

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`: 21 Curl.Protocol.Http.UnitTests fail unmutated: the verbose 'auth using' line appends the user's password.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Invoke-MutationTest.ps1's HTTP baseline left out 21 tests that fail on the unmutated tree, every one about the auth-using line (HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser, HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest, ExecuteAsync_NtlmVerbose_WritesServerAuthUsingNtlmBeforeBothRequests, ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407 and 15 more verbose auth tests). Line 28 builds the line as $"{(request.IsProxy ? "Proxy" : "Server")} auth using {scheme} with user '{request.Credential?.UserName}' and password '{request.Credential?.Password}'". The test run shows: expected "Server auth using Digest with user 'u'", actual "Server auth using Digest with user 'u' and password 'p'". curl writes only the user, so -v stderr bytes differ from curl and the password is written to the terminal. The tests are right and the product is wrong; the mutation runs could only proceed by leaving these tests out.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~HttpAuthUsingLinesTests|Name=ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest|Name=ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407"
```

- Expected: Passed! - Failed: 0, Passed: 19
- Actual: Failed! - Failed: 12, Passed: 7, Total: 19; e.g. expected "Server auth using Digest with user 'u'", actual "Server auth using Digest with user 'u' and password 'p'"

## Re-audits

## Log

- 2026-10-10: filed proposed.

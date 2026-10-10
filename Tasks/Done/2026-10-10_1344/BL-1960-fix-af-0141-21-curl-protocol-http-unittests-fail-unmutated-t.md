---
id: BL-1960
title: Fix AF-0141: 21 Curl.Protocol.Http.UnitTests fail unmutated: the verbose 'auth using' line appends the user's password
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1960 — Fix AF-0141: 21 Curl.Protocol.Http.UnitTests fail unmutated: the verbose 'auth using' line appends the user's password

## Goal

The defect the audit office reported as AF-0141 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0141 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0141-21-curl-protocol-http-unittests-fail-unmutated-the.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Invoke-MutationTest.ps1's HTTP baseline left out 21 tests that fail on the unmutated tree, every one about the auth-using line (HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser, HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest, ExecuteAsync_NtlmVerbose_WritesServerAuthUsingNtlmBeforeBothRequests, ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407 and 15 more verbose auth tests). Line 28 builds the line as $"{(request.IsProxy ? "Proxy" : "Server")} auth using {scheme} with user '{request.Credential?.UserName}' and password '{request.Credential?.Password}'". The test run shows: expected "Server auth using Digest with user 'u'", actual "Server auth using Digest with user 'u' and password 'p'". curl writes only the user, so -v stderr bytes differ from curl and the password is written to the terminal. The tests are right and the product is wrong; the mutation runs could only proceed by leaving these tests out.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~HttpAuthUsingLinesTests|Name=ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest|Name=ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407"
```

- Expected: Passed! - Failed: 0, Passed: 19
- Actual: Failed! - Failed: 12, Passed: 7, Total: 19; e.g. expected "Server auth using Digest with user 'u'", actual "Server auth using Digest with user 'u' and password 'p'"

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-10: No product change was needed. `HttpAuthUsingLines.cs:28` on this branch already writes only `... with user '<user>'`, as curl does, and has since BL-954 (ece25cc3c); no "and password" text exists anywhere in Curl.Protocol.Http.UnitLibrary. The finding's reproduction, run here, gives the expected result: Passed! - Failed: 0, Passed: 19. The password suffix the auditor saw was most likely in the tree the mutation baseline ran on (a planted defect or a leftover mutant), not in the code; a lane may not read the audit office's files to confirm which. The quality auditor's re-audit closes the finding.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Reproduction passes 19/19; HttpAuthUsingLines already writes only the user, as curl does; build and fast tests green

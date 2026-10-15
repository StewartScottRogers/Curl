---
id: BL-2253
title: Fix AF-0216: HttpAuthUsingLines.AuthUsing writes the password into the -v 'auth using' line; 21 Curl.Protocol.Http unit tests fail unmutated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2253 — Fix AF-0216: HttpAuthUsingLines.AuthUsing writes the password into the -v 'auth using' line; 21 Curl.Protocol.Http unit tests fail unmutated

## Goal

The defect the audit office reported as AF-0216 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0216 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0216-httpauthusinglines-authusing-writes-the-password-i.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Line 28 formats "... auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'". The doc comment above it and curl's line name only the user. The Curl.Protocol.Http mutation baseline had to exclude 21 failing tests, among them HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser and HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest. Assertion output: expected "Server auth using Basic with user 'u'", actual "Server auth using Basic with user 'u' password 'p'". Every -v transfer with -u or -U would print the credential's password to stderr.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HttpAuthUsingLinesTests"
```

- Expected: Passed! - Failed: 0
- Actual: Failed: expected "Proxy auth using Basic with user 'pu'", actual "Proxy auth using Basic with user 'pu' password 'pp'" (and the same for Server, NTLM, Digest and Bearer)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-14 (Claude): AF-0216 is most likely audit 20261014-181528's own planted defect: its manifest plants a password appended to the verbose 'auth using' line in Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs, which the 21 failing tests catch. CI was green on the audited commit. Check that the real tree's HttpAuthUsingLines never writes the password and the Curl.Protocol.Http tests pass; if both hold, there is nothing to fix: record that and finish.

## Log

- 2026-10-14: Created.

---
id: AF-0216
title: HttpAuthUsingLines.AuthUsing writes the password into the -v 'auth using' line; 21 Curl.Protocol.Http unit tests fail unmutated
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-15_0156.md: the reproduction no longer reproduces.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:AuthUsing:failing-test
reproduction: none
task: BL-2253
tasks: BL-2253
found: 2026-10-14
found-at: d86a80f0091fbf88c08d7b9b2fa37a9197854bd8
scorecard: 2026-10-14_1815.md
duplicate-of:
closed: 2026-10-15
closed-how: reliable-reaudit
closed-by: 2026-10-15_0156.md
---
# AF-0216 - HttpAuthUsingLines.AuthUsing writes the password into the -v 'auth using' line; 21 Curl.Protocol.Http unit tests fail unmutated

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`: HttpAuthUsingLines.AuthUsing writes the password into the -v 'auth using' line; 21 Curl.Protocol.Http unit tests fail unmutated. Reappeared; previously AF-0174. Reported by an auditor flagged unreliable in 2026-10-14_1815.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Line 28 formats "... auth using {scheme} with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'". The doc comment above it and curl's line name only the user. The Curl.Protocol.Http mutation baseline had to exclude 21 failing tests, among them HttpAuthUsingLinesTests.AuthUsing_OriginRequest_WritesCurlsLine, AuthUsing_ProxyRequest_WritesCurlsLine, AuthUsing_BearerWithoutUser_NamesNoUser and HttpProtocolHandlerTests.ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest. Assertion output: expected "Server auth using Basic with user 'u'", actual "Server auth using Basic with user 'u' password 'p'". Every -v transfer with -u or -U would print the credential's password to stderr.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HttpAuthUsingLinesTests"
```

- Expected: Passed! - Failed: 0
- Actual: Failed: expected "Proxy auth using Basic with user 'pu'", actual "Proxy auth using Basic with user 'pu' password 'pp'" (and the same for Server, NTLM, Digest and Bearer)

## Re-audits

- 2026-10-15 | 2026-10-15_0156.md | reproduces: no | Ran dotnet test Curl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HttpAuthUsingLinesTests": Passed! - Failed: 0, Passed: 17, Skipped: 0, Total: 17. None of the filtered tests fail unmutated (the finding reported 21 failing).

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
- 2026-10-15: accepted -> closed. Re-audit 2026-10-15_0156.md: the reproduction no longer reproduces.

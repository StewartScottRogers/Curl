---
id: BL-3228
title: Fix AF-0362: Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode never checks the Digest method or the credentials
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3228 — Fix AF-0362: Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode never checks the Digest method or the credentials

## Goal

The defect the audit office reported as AF-0362 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0362 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0362-lib1568-performswiththedigestcredentialsandanswers.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:256`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:256`

Step 1 sample. The fake engine captures only o.Http.UserAgent; the asserts are Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode) and Assert.AreEqual("lib1568", userAgent). Lib1568.cs:69-71 sets CURLOPT_USERPWD "testuser:testpass" and CURLOPT_HTTPAUTH Digest, but no test in the file asserts either (grep for testuser:testpass and HttpAuthenticationMethods.Digest finds nothing). The port could drop the credentials or switch to Basic and the test would still pass. This is the same gap AF-0346 had in Lib1582.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode' -Context 0,10
```

- Expected: The body asserts the performed Authentication options: HttpAuthentication Digest and UserPassword testuser:testpass.
- Actual: The body asserts only the exit code and the User-Agent "lib1568".

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

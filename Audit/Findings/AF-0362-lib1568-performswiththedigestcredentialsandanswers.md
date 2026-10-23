---
id: AF-0362
title: Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode never checks the Digest method or the credentials
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode:name-lies
reproduction: none
task: BL-3228
tasks: BL-3228
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0362 - Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode never checks the Digest method or the credentials

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:256`: Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode never checks the Digest method or the credentials. Reported by an auditor flagged unreliable in 2026-10-22_1902.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:256`

Step 1 sample. The fake engine captures only o.Http.UserAgent; the asserts are Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode) and Assert.AreEqual("lib1568", userAgent). Lib1568.cs:69-71 sets CURLOPT_USERPWD "testuser:testpass" and CURLOPT_HTTPAUTH Digest, but no test in the file asserts either (grep for testuser:testpass and HttpAuthenticationMethods.Digest finds nothing). The port could drop the credentials or switch to Basic and the test would still pass. This is the same gap AF-0346 had in Lib1582.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1568_PerformsWithTheDigestCredentialsAndAnswersItsCode' -Context 0,10
```

- Expected: The body asserts the performed Authentication options: HttpAuthentication Digest and UserPassword testuser:testpass.
- Actual: The body asserts only the exit code and the User-Agent "lib1568".

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.

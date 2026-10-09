---
id: AF-0134
title: Find_SyntaxError_CarriesCurlsMessageAndExitCode asserts nothing: its only assertion is commented out
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Authentication.UnitTests/NetrcFileTests.cs:Find_SyntaxError_CarriesCurlsMessageAndExitCode:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0134 - Find_SyntaxError_CarriesCurlsMessageAndExitCode asserts nothing: its only assertion is commented out

## Summary

Medium finding from the quality auditor at `Curl.Authentication.UnitTests/NetrcFileTests.cs:170`: Find_SyntaxError_CarriesCurlsMessageAndExitCode asserts nothing: its only assertion is commented out. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `Curl.Authentication.UnitTests/NetrcFileTests.cs:170`

The test builds actual = $"curl: ({(int)result.ExitCode}) {NetrcLookupResult.SyntaxErrorMessage}" and passes it only to diagnostics.Diff, which just logs. TestDiagnostics.Diff only writes a DIFF line and increments AssertCount (TestDiagnostics.cs:214-218). Lines 180-183 hold the real check commented out: '// diagnostics.Assert(...)' and '// Assert.AreEqual("curl: (26) .netrc error: syntax error", actual);'. The name promises curl's message and exit code 26, but any exit code or message passes, as does a Find that reports no syntax error at all. Find-WeakTests.ps1 flagged it as no-assertion.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/NetrcFileTests.cs -SimpleMatch -Pattern '// Assert.AreEqual('
```

- Expected: No match: the test asserts "curl: (26) .netrc error: syntax error" and the exit code with a live Assert.
- Actual: NetrcFileTests.cs:181:        // Assert.AreEqual(

## Re-audits

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.

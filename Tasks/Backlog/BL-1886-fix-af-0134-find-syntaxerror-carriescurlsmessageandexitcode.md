---
id: BL-1886
title: Fix AF-0134: Find_SyntaxError_CarriesCurlsMessageAndExitCode asserts nothing: its only assertion is commented out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1886 — Fix AF-0134: Find_SyntaxError_CarriesCurlsMessageAndExitCode asserts nothing: its only assertion is commented out

## Goal

The defect the audit office reported as AF-0134 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0134 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0134-find-syntaxerror-carriescurlsmessageandexitcode-as.md`.

Location: `Curl.Authentication.UnitTests/NetrcFileTests.cs:170`

Location: `Curl.Authentication.UnitTests/NetrcFileTests.cs:170`

The test builds actual = $"curl: ({(int)result.ExitCode}) {NetrcLookupResult.SyntaxErrorMessage}" and passes it only to diagnostics.Diff, which just logs. TestDiagnostics.Diff only writes a DIFF line and increments AssertCount (TestDiagnostics.cs:214-218). Lines 180-183 hold the real check commented out: '// diagnostics.Assert(...)' and '// Assert.AreEqual("curl: (26) .netrc error: syntax error", actual);'. The name promises curl's message and exit code 26, but any exit code or message passes, as does a Find that reports no syntax error at all. Find-WeakTests.ps1 flagged it as no-assertion.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/NetrcFileTests.cs -SimpleMatch -Pattern '// Assert.AreEqual('
```

- Expected: No match: the test asserts "curl: (26) .netrc error: syntax error" and the exit code with a live Assert.
- Actual: NetrcFileTests.cs:181:        // Assert.AreEqual(

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.

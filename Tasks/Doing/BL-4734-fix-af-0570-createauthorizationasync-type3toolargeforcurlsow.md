---
id: BL-4734
title: Fix AF-0570: CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100 never checks exit 100
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4734 — Fix AF-0570: CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100 never checks exit 100

## Goal

The defect the audit office reported as AF-0570 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0570 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0570-createauthorizationasync-type3toolargeforcurlsownn.md`.

Location: `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs:189`

Location: `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs:189`

Step 0 name-lies candidate. The body takes the HttpAuthenticationFailedException (which carries ExitCode, Curl.Protocol.Abstractions.UnitLibrary/HttpAuthenticationFailedException.cs:19) as `failure` but asserts only Assert.IsEmpty(events.Info); failure.ExitCode (CurlExitCode.TooLarge, 100) is never compared, so a refusal with any other exit code passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs -Pattern 'ReportsNothingBeforeExit100' -Context 0,33 | Out-String | Select-String 'ExitCode'
```

- Expected: A match: the test asserts failure.ExitCode is CurlExitCode.TooLarge (100).
- Actual: No match: the body's only assertion is Assert.IsEmpty(events.Info);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the missing ExitCode == CurlExitCode.TooLarge assertion to the test; test passes.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

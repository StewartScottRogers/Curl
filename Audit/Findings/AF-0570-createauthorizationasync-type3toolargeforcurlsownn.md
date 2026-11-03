---
id: AF-0570
title: CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100 never checks exit 100
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs:CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0570 - CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100 never checks exit 100

## Summary

High finding from the quality auditor at `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs:189`: CreateAuthorizationAsync_Type3TooLargeForCurlsOwnNtlm_ReportsNothingBeforeExit100 never checks exit 100. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs:189`

Step 0 name-lies candidate. The body takes the HttpAuthenticationFailedException (which carries ExitCode, Curl.Protocol.Abstractions.UnitLibrary/HttpAuthenticationFailedException.cs:19) as `failure` but asserts only Assert.IsEmpty(events.Info); failure.ExitCode (CurlExitCode.TooLarge, 100) is never compared, so a refusal with any other exit code passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.VerboseLines.cs -Pattern 'ReportsNothingBeforeExit100' -Context 0,33 | Out-String | Select-String 'ExitCode'
```

- Expected: A match: the test asserts failure.ExitCode is CurlExitCode.TooLarge (100).
- Actual: No match: the body's only assertion is Assert.IsEmpty(events.Info);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.

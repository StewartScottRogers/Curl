---
id: BL-4896
title: Fix AF-0702: ADR-0139 says DIGEST-MD5 challenges SSPI rejects are answered null (exit 67) because exit 94 cannot be expressed; SaslAuthenticator now raises exit 94 for them
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-06
completed:
---
# BL-4896 — Fix AF-0702: ADR-0139 says DIGEST-MD5 challenges SSPI rejects are answered null (exit 67) because exit 94 cannot be expressed; SaslAuthenticator now raises exit 94 for them

## Goal

The defect the audit office reported as AF-0702 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0702 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0702-adr-0139-says-digest-md5-challenges-sspi-rejects-a.md`.

Location: `Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md:47`

Location: `Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md:47`

Found by Find-AdrNumberMismatches.ps1 (exit-not-in-code: 'the Decision states exit 67; the code it names (SaslAuthenticator) produces exit 94 and never 67'). ADR-0139 (Accepted) Decision, lines 47-49: 'SSPI's exit 94 cannot yet be expressed: `ISaslExchange.Respond` only answers bytes or `null`, which the handlers turn into `*` and exit 67. Until BL-781 widens that contract, the three challenges SSPI rejects are answered `null`.' The throw site now raises 94 for exactly that outcome: Curl.Authentication.UnitLibrary/SaslAuthenticator.cs:207-220, comment 'A challenge SSPI rejects fails the transfer with exit 94 rather than cancelling it (BL-781)', DigestMd5Answer: 'SaslDigestMd5.AnswerAsSspi(...) ?? throw DigestMd5Failed()', and DigestMd5Failed returns 'new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage)' (AuthError = 94, message 'An authentication function returned an error'). So the ADR's stated outcome (null answer, '*', exit 67) is no longer what the code does, and an agent would expect exit 67 where Curl gives 94. Phase 2: the ADR itself names BL-781 as the change that would lift the limit; the code comment cites BL-781 as done, but the ADR was not amended.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md -Pattern 'cannot yet be expressed|are answered `null`'; Select-String -Path Curl.Authentication.UnitLibrary/SaslAuthenticator.cs -Pattern 'throw DigestMd5Failed|new SaslAuthenticationFailedException\(CurlExitCode.AuthError'
```

- Expected: ADR-0139 states that a DIGEST-MD5 challenge SSPI rejects fails with exit 94 (CURLE_AUTH_ERROR, 'An authentication function returned an error'), as SaslAuthenticator does; no 'cannot yet be expressed' or 'answered `null`' sentence.
- Actual: ADR-0139:47 'SSPI's exit 94 cannot yet be expressed ... exit 67' and :49 'the three challenges SSPI rejects are answered `null`'; SaslAuthenticator.cs:213 '?? throw DigestMd5Failed()' and :220 'new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage)'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.

---
id: AF-0702
title: ADR-0139 says DIGEST-MD5 challenges SSPI rejects are answered null (exit 67) because exit 94 cannot be expressed; SaslAuthenticator now raises exit 94 for them
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md:ADR-0139:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0702 - ADR-0139 says DIGEST-MD5 challenges SSPI rejects are answered null (exit 67) because exit 94 cannot be expressed; SaslAuthenticator now raises exit 94 for them

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md:47`: ADR-0139 says DIGEST-MD5 challenges SSPI rejects are answered null (exit 67) because exit 94 cannot be expressed; SaslAuthenticator now raises exit 94 for them.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md:47`

Found by Find-AdrNumberMismatches.ps1 (exit-not-in-code: 'the Decision states exit 67; the code it names (SaslAuthenticator) produces exit 94 and never 67'). ADR-0139 (Accepted) Decision, lines 47-49: 'SSPI's exit 94 cannot yet be expressed: `ISaslExchange.Respond` only answers bytes or `null`, which the handlers turn into `*` and exit 67. Until BL-781 widens that contract, the three challenges SSPI rejects are answered `null`.' The throw site now raises 94 for exactly that outcome: Curl.Authentication.UnitLibrary/SaslAuthenticator.cs:207-220, comment 'A challenge SSPI rejects fails the transfer with exit 94 rather than cancelling it (BL-781)', DigestMd5Answer: 'SaslDigestMd5.AnswerAsSspi(...) ?? throw DigestMd5Failed()', and DigestMd5Failed returns 'new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage)' (AuthError = 94, message 'An authentication function returned an error'). So the ADR's stated outcome (null answer, '*', exit 67) is no longer what the code does, and an agent would expect exit 67 where Curl gives 94. Phase 2: the ADR itself names BL-781 as the change that would lift the limit; the code comment cites BL-781 as done, but the ADR was not amended.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0139-sasl-cram-md5-and-digest-md5-answer-as-each-platforms-curl-with-sspi-on-windows.md -Pattern 'cannot yet be expressed|are answered `null`'; Select-String -Path Curl.Authentication.UnitLibrary/SaslAuthenticator.cs -Pattern 'throw DigestMd5Failed|new SaslAuthenticationFailedException\(CurlExitCode.AuthError'
```

- Expected: ADR-0139 states that a DIGEST-MD5 challenge SSPI rejects fails with exit 94 (CURLE_AUTH_ERROR, 'An authentication function returned an error'), as SaslAuthenticator does; no 'cannot yet be expressed' or 'answered `null`' sentence.
- Actual: ADR-0139:47 'SSPI's exit 94 cannot yet be expressed ... exit 67' and :49 'the three challenges SSPI rejects are answered `null`'; SaslAuthenticator.cs:213 '?? throw DigestMd5Failed()' and :220 'new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage)'.

## Re-audits

## Log

- 2026-11-05: filed proposed.

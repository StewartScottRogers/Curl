---
id: AF-0764
title: Judge_RevokedStapleWithNoCertificateSent_IsIgnored never asserts the message, only that it is not the revoked one
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/Cgr014Tests.cs:Judge_RevokedStapleWithNoCertificateSent_IsIgnored:weak-assertion
reproduction: none
task: BL-5213
tasks: BL-5213
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: reliable-reaudit
closed-by: 2026-11-15_0222.md
---
# AF-0764 - Judge_RevokedStapleWithNoCertificateSent_IsIgnored never asserts the message, only that it is not the revoked one

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/Cgr014Tests.cs:121`: Judge_RevokedStapleWithNoCertificateSent_IsIgnored never asserts the message, only that it is not the revoked one. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Networking.UnitTests/Cgr014Tests.cs:121`

Line 121 is Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message) beside an exit-code check. The message for a server that sent no certificate is never pinned, so any wrong text other than the revoked line passes. This is the same pattern as AF-0749, which was fixed elsewhere by asserting the exact text.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/Cgr014Tests.cs -Pattern 'Judge_RevokedStapleWithNoCertificateSent_IsIgnored' -Context 0,10
```

- Expected: An Assert.AreEqual of the exact no-certificate message.
- Actual: Only Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message) checks the message.

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the Select-String. Line 121 asserts Assert.AreEqual(TlsFailureMessages.SchannelCaCertificateFileChainError(X509ChainStatusFlags.NoError), failure?.Message).

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.

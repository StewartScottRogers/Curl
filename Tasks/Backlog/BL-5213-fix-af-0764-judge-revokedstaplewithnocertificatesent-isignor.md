---
id: BL-5213
title: Fix AF-0764: Judge_RevokedStapleWithNoCertificateSent_IsIgnored never asserts the message, only that it is not the revoked one
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-5213 — Fix AF-0764: Judge_RevokedStapleWithNoCertificateSent_IsIgnored never asserts the message, only that it is not the revoked one

## Goal

The defect the audit office reported as AF-0764 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0764 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/Cgr014Tests.cs:121`

Location: `Curl.Networking.UnitTests/Cgr014Tests.cs:121`

Line 121 is Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message) beside an exit-code check. The message for a server that sent no certificate is never pinned, so any wrong text other than the revoked line passes. This is the same pattern as AF-0749, which was fixed elsewhere by asserting the exact text.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/Cgr014Tests.cs -Pattern 'Judge_RevokedStapleWithNoCertificateSent_IsIgnored' -Context 0,10
```

- Expected: An Assert.AreEqual of the exact no-certificate message.
- Actual: Only Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message) checks the message.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.

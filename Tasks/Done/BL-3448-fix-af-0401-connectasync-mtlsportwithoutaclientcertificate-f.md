---
id: BL-3448
title: Fix AF-0401: ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake checks only that the reply is not the expected one, not that the handshake failed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-24
completed: 2026-10-24
---
# BL-3448 — Fix AF-0401: ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake checks only that the reply is not the expected one, not that the handshake failed

## Goal

The defect the audit office reported as AF-0401 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0401 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0401-connectasync-mtlsportwithoutaclientcertificate-fai.md`.

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:49`

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:49`

The assertions are Assert.AreNotEqual(ExpectedReply, reply); and Assert.IsEmpty(verifier.ReceivedCertificates);. A server that completed the handshake without asking for a certificate and answered with any other bytes, or one that answered nothing over an established connection, also passes; the name promises a failed handshake, which the test never observes (reply is null or the exchange fails).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs -Pattern 'Assert.AreNotEqual\(ExpectedReply, reply\)'
```

- Expected: No match: the test asserts the handshake failure itself (e.g. reply is null).
- Actual: HttpsServerConnectorTests.Mtls.cs:49:        Assert.AreNotEqual(ExpectedReply, reply);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The two mTLS failure tests now assert `Assert.IsNull(reply)`; TryExchangeAsync returns null also when the server closes with no reply byte, so a clean close counts as a failed exchange. Build clean, Curl.Conformance.UnitTests fast tests 4360 passed, 0 failed.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Done. mTLS failure tests assert a null reply (failed handshake) instead of not-the-expected-reply

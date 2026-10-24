---
id: AF-0401
title: ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake checks only that the reply is not the expected one, not that the handshake failed
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0401 - ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake checks only that the reply is not the expected one, not that the handshake failed

## Summary

Low finding from the quality auditor at `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:49`: ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake checks only that the reply is not the expected one, not that the handshake failed. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:49`

The assertions are Assert.AreNotEqual(ExpectedReply, reply); and Assert.IsEmpty(verifier.ReceivedCertificates);. A server that completed the handshake without asking for a certificate and answered with any other bytes, or one that answered nothing over an established connection, also passes; the name promises a failed handshake, which the test never observes (reply is null or the exchange fails).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs -Pattern 'Assert.AreNotEqual\(ExpectedReply, reply\)'
```

- Expected: No match: the test asserts the handshake failure itself (e.g. reply is null).
- Actual: HttpsServerConnectorTests.Mtls.cs:49:        Assert.AreNotEqual(ExpectedReply, reply);

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.

---
id: AF-0527
title: The mTLS refusal tests pass on any exception: TryExchangeAsync turns every failure into null, the value the refusal tests assert
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake:swallowed-failure
reproduction: none
task: BL-4511
tasks: BL-4511
found: 2026-11-02
found-at: 4698726450f0ce3447bd6790d0bf9c59787bae95
scorecard: 2026-11-02_0645.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0527 - The mTLS refusal tests pass on any exception: TryExchangeAsync turns every failure into null, the value the refusal tests assert

## Summary

Low finding from the quality auditor at `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:95`: The mTLS refusal tests pass on any exception: TryExchangeAsync turns every failure into null, the value the refusal tests assert. Reported by an auditor flagged unreliable in 2026-11-02_0645.md.

## Evidence

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:95`

Step 2 catch-all search. TryExchangeAsync (line 79) wraps the TLS handshake and exchange in 'catch (Exception) { return null; }' (lines 95-98), with a CA1031 suppression justified as 'Any failure of a refused handshake is the outcome under test'. ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake then asserts only 'Assert.IsNull(reply); Assert.IsEmpty(verifier.ReceivedCertificates);'. Any failure, such as a NullReferenceException in the server connector, a broken certificate load or a port that never answers, gives null and an empty verifier, so the test passes without a handshake being refused for the missing certificate. The sibling ConnectAsync_MtlsPortWithACertificateTheTestCaDidNotSign_FailsTheHandshakeAndRecordsIt is partly protected by its HasCount(1, ReceivedCertificates) check.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs -Pattern 'catch \(Exception\)' -Context 0,3
```

- Expected: The refusal test catches only the handshake failure it expects (AuthenticationException or IOException from the TLS layer), or asserts its type, so an unrelated exception fails the test.
- Actual: HttpsServerConnectorTests.Mtls.cs:95: catch (Exception) { return null; }

## Re-audits

- 2026-11-02 | 2026-11-02_1724.md | reproduces: no | Ran the reproduction: Select-String -Pattern 'catch \(Exception\)' on Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs found no match. TryExchangeAsync now has 'catch (Exception exception) when (exception is AuthenticationException or IOException)', and its comment says any other exception escapes and fails the test (AF-0527). The catch-all that turned every failure into null is gone.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.

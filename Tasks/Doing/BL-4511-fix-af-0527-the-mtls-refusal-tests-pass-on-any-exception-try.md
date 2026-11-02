---
id: BL-4511
title: Fix AF-0527: The mTLS refusal tests pass on any exception: TryExchangeAsync turns every failure into null, the value the refusal tests assert
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-11-02
completed:
---
# BL-4511 — Fix AF-0527: The mTLS refusal tests pass on any exception: TryExchangeAsync turns every failure into null, the value the refusal tests assert

## Goal

The defect the audit office reported as AF-0527 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0527 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0527-the-mtls-refusal-tests-pass-on-any-exception-tryex.md`.

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:95`

Location: `Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs:95`

Step 2 catch-all search. TryExchangeAsync (line 79) wraps the TLS handshake and exchange in 'catch (Exception) { return null; }' (lines 95-98), with a CA1031 suppression justified as 'Any failure of a refused handshake is the outcome under test'. ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake then asserts only 'Assert.IsNull(reply); Assert.IsEmpty(verifier.ReceivedCertificates);'. Any failure, such as a NullReferenceException in the server connector, a broken certificate load or a port that never answers, gives null and an empty verifier, so the test passes without a handshake being refused for the missing certificate. The sibling ConnectAsync_MtlsPortWithACertificateTheTestCaDidNotSign_FailsTheHandshakeAndRecordsIt is partly protected by its HasCount(1, ReceivedCertificates) check.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs -Pattern 'catch \(Exception\)' -Context 0,3
```

- Expected: The refusal test catches only the handshake failure it expects (AuthenticationException or IOException from the TLS layer), or asserts its type, so an unrelated exception fails the test.
- Actual: HttpsServerConnectorTests.Mtls.cs:95: catch (Exception) { return null; }

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.

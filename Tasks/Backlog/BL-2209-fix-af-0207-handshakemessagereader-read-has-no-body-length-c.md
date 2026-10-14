---
id: BL-2209
title: Fix AF-0207: HandshakeMessageReader.Read has no body-length check: a split handshake message throws ArgumentOutOfRangeException and 6 tests fail
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2209 — Fix AF-0207: HandshakeMessageReader.Read has no body-length check: a split handshake message throws ArgumentOutOfRangeException and 6 tests fail

## Goal

The defect the audit office reported as AF-0207 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0207 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0207-handshakemessagereader-read-has-no-body-length-che.md`.

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:31`

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:31`

Lines 30-32: int length = (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]; int total = HandshakeMessage.HeaderLength + length; return HandshakeMessageReadResult.Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total);. Nothing checks buffer.Length < total. The doc comment promises NeedMoreBytes 'when the header or body is not all there yet'. The slice throws instead. Every caller frames received bytes with it: Tls13ClientHandshake.cs:347, Tls12ClientHandshake.cs:289, TlsClientConnection.cs:95, Tls13ClientStream.cs:256 and Tls12ClientStream.cs:238. So a server flight split across records or CRYPTO frames crashes the client. Failing tests on the audited tree: Curl.Tls.UnitTests HandshakeMessageReaderTests.ReadWaitsForAHeaderOrBodyThatIsNotAllThere, both rows ("14000020" and the 34-byte row), with System.ArgumentOutOfRangeException. Curl.Networking.UnitTests TcpConnectorQuicTests: ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello, ConnectMultiplexedAsync_OnTheOpenSslBuildWithoutCaCert_ReportsTheBuildsDefaultBundle, ConnectMultiplexedAsync_WithCertWhenTheServerAsksForOne_PresentsTheCertificate and ConnectMultiplexedAsync_WithTls13Ciphers_OffersOnlyTheSuitesItNames. All four throw ArgumentOutOfRangeException at HandshakeMessageReader.cs:line 32, reached through QuicTestServer.ReceiveCrypto when the OpenSSL-profile ClientHello arrives in more than one CRYPTO frame. Invoke-MutationTest.ps1 -ExcludeBaselineFailures left out the same four Networking tests.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~HandshakeMessageReaderTests"
```

- Expected: Passed! - Failed: 0, Passed: 9; a partial header or body returns NeedMoreBytes.
- Actual: Failed! - Failed: 2, Passed: 7, Total: 9; ReadWaitsForAHeaderOrBodyThatIsNotAllThere ("14000020") threw System.ArgumentOutOfRangeException.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.

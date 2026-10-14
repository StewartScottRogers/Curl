---
id: AF-0207
title: HandshakeMessageReader.Read has no body-length check: a split handshake message throws ArgumentOutOfRangeException and 6 tests fail
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:Read:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0207 - HandshakeMessageReader.Read has no body-length check: a split handshake message throws ArgumentOutOfRangeException and 6 tests fail

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:31`: HandshakeMessageReader.Read has no body-length check: a split handshake message throws ArgumentOutOfRangeException and 6 tests fail.

## Evidence

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:31`

Lines 30-32: int length = (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]; int total = HandshakeMessage.HeaderLength + length; return HandshakeMessageReadResult.Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total);. Nothing checks buffer.Length < total. The doc comment promises NeedMoreBytes 'when the header or body is not all there yet'. The slice throws instead. Every caller frames received bytes with it: Tls13ClientHandshake.cs:347, Tls12ClientHandshake.cs:289, TlsClientConnection.cs:95, Tls13ClientStream.cs:256 and Tls12ClientStream.cs:238. So a server flight split across records or CRYPTO frames crashes the client. Failing tests on the audited tree: Curl.Tls.UnitTests HandshakeMessageReaderTests.ReadWaitsForAHeaderOrBodyThatIsNotAllThere, both rows ("14000020" and the 34-byte row), with System.ArgumentOutOfRangeException. Curl.Networking.UnitTests TcpConnectorQuicTests: ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello, ConnectMultiplexedAsync_OnTheOpenSslBuildWithoutCaCert_ReportsTheBuildsDefaultBundle, ConnectMultiplexedAsync_WithCertWhenTheServerAsksForOne_PresentsTheCertificate and ConnectMultiplexedAsync_WithTls13Ciphers_OffersOnlyTheSuitesItNames. All four throw ArgumentOutOfRangeException at HandshakeMessageReader.cs:line 32, reached through QuicTestServer.ReceiveCrypto when the OpenSSL-profile ClientHello arrives in more than one CRYPTO frame. Invoke-MutationTest.ps1 -ExcludeBaselineFailures left out the same four Networking tests.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~HandshakeMessageReaderTests"
```

- Expected: Passed! - Failed: 0, Passed: 9; a partial header or body returns NeedMoreBytes.
- Actual: Failed! - Failed: 2, Passed: 7, Total: 9; ReadWaitsForAHeaderOrBodyThatIsNotAllThere ("14000020") threw System.ArgumentOutOfRangeException.

## Re-audits

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.

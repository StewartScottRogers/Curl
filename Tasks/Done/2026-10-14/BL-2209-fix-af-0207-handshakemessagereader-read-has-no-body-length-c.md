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
completed: 2026-10-14
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-14 (Claude): AF-0207 is most likely this audit's own planted defect: audit 20261014-045316's manifest plants a removed length check in Curl.Tls.UnitLibrary/HandshakeMessageReader.cs, which the 6 failing tests catch. CI was green on the audited commit. Check that the real tree's HandshakeMessageReader.Read checks the 24-bit length and the 6 tests pass; if both hold, there is nothing to fix: record that and finish.

- 2026-10-14 (Claude, lane 1): Confirmed: AF-0207 was the audit's planted defect. The real HandshakeMessageReader.Read returns NeedMoreBytes when buffer.Length < 4 + length (line 32), so no code change. The reproduction gives Passed 9/9; dotnet build is clean and the fast tests are green (Curl.Tls.UnitTests 1271 passed, Curl.Networking.UnitTests 3149 passed, the four TcpConnectorQuicTests included). The finding closes on the quality auditor's re-audit.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. AF-0207 does not reproduce: HandshakeMessageReader.Read already checks the body length; the 9 reader tests and the 4 QUIC tests pass

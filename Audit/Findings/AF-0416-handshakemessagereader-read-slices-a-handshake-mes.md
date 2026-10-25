---
id: AF-0416
title: HandshakeMessageReader.Read slices a handshake message before checking it has all arrived, so 8 Curl.Tls.UnitTests and 5 Curl.Networking QUIC tests fail unmutated
auditor: quality
severity: High
status: rejected
reason: Stewart rejected it on 2026-10-24: it is the security auditor's planted defect in HandshakeMessageReader.cs (run 20261024-084551's manifest), reported by the quality auditor; the audited commit has the length check (BL-3465 sets such findings aside).
key: quality:Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:Read:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0416 - HandshakeMessageReader.Read slices a handshake message before checking it has all arrived, so 8 Curl.Tls.UnitTests and 5 Curl.Networking QUIC tests fail unmutated

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:33`: HandshakeMessageReader.Read slices a handshake message before checking it has all arrived, so 8 Curl.Tls.UnitTests and 5 Curl.Networking QUIC tests fail unmutated. Reappeared; previously AF-0207.

## Evidence

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:33`

Read checks buffer.Length < HeaderLength, then computes total = HeaderLength + length and returns Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total) with no check that buffer.Length >= total. A handshake message split across records, or a header announcing a longer body, throws ArgumentOutOfRangeException instead of answering NeedMoreBytes. dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" on the audited tree: Passed 1266, Failed 8 (ReadWaitsForAHeaderOrBodyThatIsNotAllThere x2, SimpleHandshakeTakesTheServerFlightOneByteAtATime, MessagesSplitAcrossAndJoinedWithinRecordsAreReassembled, NewSessionTicketSplitAcrossRecordsReachesTheHandshake, HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing, HandshakeReader_MessageFragmentedByteByByte_CompletesOnlyOnTheLastByte, Tls12ServerContinuesAsTls12AndExchangesData), every one System.ArgumentOutOfRangeException at HandshakeMessageReader.Read line 32. The test host then crashed in Tls12ClientConnectionTests.ServerFlightSplitAcrossRecordsCompletesTheHandshake (Test Run Aborted). The mutation baseline of Curl.Networking.UnitLibrary also had to leave out 5 failing TcpConnectorQuicTests (ConnectMultiplexedAsync_InTheOpenSslBuild_SendsTheOpenSslProfilesTls13ClientHello and four more). In the product, a real server whose TLS flight spans records breaks the hand-built TLS handshake.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing"
```

- Expected: Passed: the reader answers NeedMoreBytes for a header whose body has not arrived.
- Actual: Failed: System.ArgumentOutOfRangeException at Curl.Tls.HandshakeMessageReader.Read (HandshakeMessageReader.cs:line 32).

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> rejected.

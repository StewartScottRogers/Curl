---
id: AF-0270
title: HandshakeMessageReader.Read slices a body that has not all arrived; Curl.Tls unit tests fail unmutated
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:Read:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0270 - HandshakeMessageReader.Read slices a body that has not all arrived; Curl.Tls unit tests fail unmutated

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:32`: HandshakeMessageReader.Read slices a body that has not all arrived; Curl.Tls unit tests fail unmutated. Reappeared; previously AF-0207. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:32`

Read computes total = HeaderLength + length and returns Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total) with no check that buffer.Length >= total, although its doc comment promises NeedMoreBytes 'when the header or body is not all there yet'. A message split across records throws ArgumentOutOfRangeException. dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" on the unmutated tree: 8 failures (ReadWaitsForAHeaderOrBodyThatIsNotAllThere x2, HandshakeReader_MessageFragmentedByteByByte_CompletesOnlyOnTheLastByte, HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing, MessagesSplitAcrossAndJoinedWithinRecordsAreReassembled, NewSessionTicketSplitAcrossRecordsReachesTheHandshake, SimpleHandshakeTakesTheServerFlightOneByteAtATime, Tls12ServerContinuesAsTls12AndExchangesData(7)), every stack topped by Curl.Tls.HandshakeMessageReader.Read line 32; the run then hung and was stopped. The Networking mutation baseline also had to exclude 4 TcpConnectorQuicTests.ConnectMultiplexedAsync_* tests that fail unmutated (not traced to a cause). This is not AF-0231: TlsReader.ReadVector now bounds-checks through Take.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~HandshakeMessageReaderTests"
```

- Expected: All HandshakeMessageReaderTests pass: a short body answers NeedMoreBytes.
- Actual: ReadWaitsForAHeaderOrBodyThatIsNotAllThere fails with System.ArgumentOutOfRangeException at HandshakeMessageReader.Read line 32.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.

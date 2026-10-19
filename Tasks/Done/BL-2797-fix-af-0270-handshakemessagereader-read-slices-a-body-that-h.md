---
id: BL-2797
title: Fix AF-0270: HandshakeMessageReader.Read slices a body that has not all arrived; Curl.Tls unit tests fail unmutated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2797 — Fix AF-0270: HandshakeMessageReader.Read slices a body that has not all arrived; Curl.Tls unit tests fail unmutated

## Goal

The defect the audit office reported as AF-0270 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0270 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0270-handshakemessagereader-read-slices-a-body-that-has.md`.

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:32`

Location: `Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:32`

Read computes total = HeaderLength + length and returns Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total) with no check that buffer.Length >= total, although its doc comment promises NeedMoreBytes 'when the header or body is not all there yet'. A message split across records throws ArgumentOutOfRangeException. dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" on the unmutated tree: 8 failures (ReadWaitsForAHeaderOrBodyThatIsNotAllThere x2, HandshakeReader_MessageFragmentedByteByByte_CompletesOnlyOnTheLastByte, HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing, MessagesSplitAcrossAndJoinedWithinRecordsAreReassembled, NewSessionTicketSplitAcrossRecordsReachesTheHandshake, SimpleHandshakeTakesTheServerFlightOneByteAtATime, Tls12ServerContinuesAsTls12AndExchangesData(7)), every stack topped by Curl.Tls.HandshakeMessageReader.Read line 32; the run then hung and was stopped. The Networking mutation baseline also had to exclude 4 TcpConnectorQuicTests.ConnectMultiplexedAsync_* tests that fail unmutated (not traced to a cause). This is not AF-0231: TlsReader.ReadVector now bounds-checks through Take.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~HandshakeMessageReaderTests"
```

- Expected: All HandshakeMessageReaderTests pass: a short body answers NeedMoreBytes.
- Actual: ReadWaitsForAHeaderOrBodyThatIsNotAllThere fails with System.ArgumentOutOfRangeException at HandshakeMessageReader.Read line 32.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-19 (lane 1): No code change needed. `HandshakeMessageReader.Read` already returns NeedMoreBytes when `buffer.Length < total` (line 32), unchanged since BL-698 (fdabbf7ed). The reproduction on this tree passes 9/9 HandshakeMessageReaderTests, and Curl.Tls.UnitTests passes 1275/1275, so the audited tree most likely carried a planted defect at that line that the auditor reported as a real one. Left for the quality auditor's re-audit to confirm and close AF-0270.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. AF-0270 reproduction passes: HandshakeMessageReader.Read answers NeedMoreBytes for a short body (check already present; no code change)

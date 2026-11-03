# CoAP requirement matrix (DTLS 1.2 faults, RFC 6347)

Curl's `coaps://` client (`Curl.Extended.Protocol.Coap.UnitLibrary`, `Dtls/` and `Secure/DtlsDatagramChannel`)
measured against RFC 6347's fragmentation, retransmission, replay, epoch and alert rules by the cases in
`Curl.Conformance.Extended.UnitTests/Coap/Dtls/`, run against `DtlsReferenceServer` with one of its fault hooks in
`Curl.Conformance.Extended.UnitLibrary/Coap/Dtls/` (BL-4574): `DtlsReferenceFragmentingFault` fragments the hello
flight and sends it last record first, `DtlsReferenceFlightDropFault` loses the first hello flight,
`DtlsReferenceReplayFault` sends a record under a sequence number curl has already received,
`DtlsReferenceOldEpochFault` sends an epoch 0 application data record after the handshake, and
`DtlsReferenceAlertFault` answers with a fatal alert. A row whose case fails is listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with its fix task.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Coap:RFC6347-4.1-2 | MUST | Once the handshake has moved to epoch 1, a record from epoch 0 is not accepted as application data: it is dropped and the real answer is taken. | CoapDtlsReplayAndEpochTests.Epoch0RecordAfterTheHandshake_CurlDropsItAndWritesTheRealAnswer |
| Coap:RFC6347-4.1-3 | MUST | Every record goes under its own sequence number in its epoch, a retransmitted one included, so a flight sent again is not discarded as a replay. | CoapDtlsRetransmissionTests.HelloFlightLost_TheRetransmittedClientHelloGoesUnderANewRecordSequenceNumber |
| Coap:RFC6347-4.1.2.6-1 | SHOULD | A record under a sequence number already received in its epoch is a duplicate and is discarded, even though it authenticates. | CoapDtlsReplayAndEpochTests.ReplayedSequenceNumberBeforeTheAnswer_CurlDropsItAndWritesTheRealAnswer |
| Coap:RFC6347-4.2.2-1 | SHOULD | Handshake messages that arrive before the one expected next are queued, not discarded, so a flight sent out of order completes. | CoapDtlsFragmentationTests.HelloFlightFragmentedAndReversed_CurlReassemblesItAndCompletesTheHandshake |
| Coap:RFC6347-4.2.3-1 | MUST | A handshake message fragmented across several records is reassembled from its fragment_offset and fragment_length. | CoapDtlsFragmentationTests.HelloFlightFragmentedAndReversed_CurlReassemblesItAndCompletesTheHandshake |
| Coap:RFC6347-4.2.4-1 | MUST | When the retransmission timer expires with no answer to a flight, the client sends its flight again in new records. | CoapDtlsRetransmissionTests.HelloFlightLost_CurlSendsItsClientHelloAgainOnTheTimerAndCompletesTheHandshake |
| Coap:RFC6347-4.2.7-1 | MUST | A fatal alert ends the handshake at once (RFC 5246 section 7.2): the client sends nothing more and exits 35. | CoapDtlsAlertTests.FatalAlertInPlaceOfTheHelloFlight_CurlStopsWithExit35AndSendsNothingMore |

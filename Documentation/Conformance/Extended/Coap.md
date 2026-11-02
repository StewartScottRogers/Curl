# CoAP requirement matrix (RFC 7252)

Curl's `coap://` client (`Curl.Extended.Protocol.Coap.UnitLibrary`) measured against RFC 7252 by the cases in
`Curl.Conformance.Extended.UnitTests/Coap/`, run against `CoapReferenceServer`
(`Curl.Conformance.Extended.UnitLibrary/Coap/`). This part (BL-4524) holds sections 3 (message format) and 4
(messaging), the requirements that bind a client; BL-4525 and BL-4526 add rows for the later sections. Where the
RFC states a rule of the retransmission algorithm without a keyword, the Level says "(unkeyworded)".

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Coap:RFC7252-3-1 | MUST | Implementations set the Version field to 1. | CoapMessageFormatTests.Get_RequestHeader_IsVersion1ConfirmableGetWithTokenAndUriOptions |
| Coap:RFC7252-3-2 | MUST | Messages with an unknown version number are silently ignored. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-3-3 | MUST NOT | Token lengths 9 to 15 are not sent. | CoapMessageFormatTests.Get_RequestHeader_IsVersion1ConfirmableGetWithTokenAndUriOptions |
| Coap:RFC7252-3-4 | MUST | A token length of 9 to 15 is processed as a message format error. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-3-5 | MUST | A payload marker followed by a zero-length payload is processed as a message format error. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-3.1-1 | MUST | An Option Delta of 15 in a byte that is not the payload marker is processed as a message format error. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-3.1-2 | MUST | An Option Length of 15 is processed as a message format error. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-3.2-1 | SHOULD | A sender represents a uint option value with as few bytes as possible. | n/a: a coap:// request of sections 3 and 4 carries only the string options Uri-Host, Uri-Path and Uri-Query; Block1 and Block2 are BL-4471's |
| Coap:RFC7252-3.2-2 | MUST | Recipients process uint values with leading zero bytes. | CoapMessageFormatTests.PiggybackedResponse_WithUintOptionsWithLeadingZeroBytes_IsWritten |
| Coap:RFC7252-4.1-1 | MUST, MUST NOT | An Empty message has token length 0 and no bytes after the Message ID. | CoapConfirmableExchangeTests.SeparateConfirmableResponse_IsWrittenAndAcknowledgedWithAnEmptyAcknowledgement |
| Coap:RFC7252-4.1-2 | MUST | Bytes after the Message ID of an Empty message are processed as a message format error. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-4.2-1 | (unkeyworded) | A Confirmable request is answered by an Acknowledgement carrying the response (piggybacked), which ends the exchange. | CoapConfirmableExchangeTests.PiggybackedResponse_IsWrittenAndNothingMoreIsSent |
| Coap:RFC7252-4.2-2 | MUST | An Acknowledgement echoes the Message ID of the Confirmable message and carries a response or is Empty. | CoapConfirmableExchangeTests.SeparateConfirmableResponse_IsWrittenAndAcknowledgedWithAnEmptyAcknowledgement |
| Coap:RFC7252-4.2-3 | MUST | A Confirmable message the recipient lacks context for is rejected with a matching Reset and otherwise ignored. | CoapConfirmableExchangeTests.UnrelatedConfirmableMessage_IsRejectedWithAnEmptyReset |
| Coap:RFC7252-4.2-4 | MUST | A Reset echoes the Message ID of the Confirmable message and is Empty. | CoapConfirmableExchangeTests.UnrelatedConfirmableMessage_IsRejectedWithAnEmptyReset |
| Coap:RFC7252-4.2-5 | (unkeyworded) | Rejecting an Acknowledgement or Reset, including one carrying a message format error, is done by silently ignoring it. | CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted |
| Coap:RFC7252-4.2-6 | (unkeyworded) | On a Reset the attempt to transmit is cancelled and the application informed of failure. | CoapConfirmableExchangeTests.Reset_FailsTheTransferWithExit56 |
| Coap:RFC7252-4.2-7 | (unkeyworded) | The initial timeout is a random duration between ACK_TIMEOUT and ACK_TIMEOUT * ACK_RANDOM_FACTOR, doubled at each retransmission. | CoapRetransmissionTests.SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28 |
| Coap:RFC7252-4.2-8 | (unkeyworded) | When the retransmission counter reaches MAX_RETRANSMIT on a timeout, the attempt is cancelled and the application informed of failure. | CoapRetransmissionTests.SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28 |
| Coap:RFC7252-4.3-1 | MUST NOT | A Non-confirmable message is not acknowledged by the recipient. | CoapConfirmableExchangeTests.SeparateNonConfirmableResponse_IsWrittenAndNotAcknowledged |
| Coap:RFC7252-4.3-2 | MUST NOT | A Non-confirmable message is not Empty. | n/a: curl sends its requests Confirmable and sends no Non-confirmable message under sections 3 and 4 (RFC 7641 re-registration is BL-4471's) |
| Coap:RFC7252-4.4-1 | MUST | A retransmission carries the same Message ID (and is the same message). | CoapRetransmissionTests.SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28 |
| Coap:RFC7252-4.4-2 | MUST NOT | A Message ID is not reused with the same endpoint within EXCHANGE_LIFETIME. | n/a: a coap:// run without block-wise transfer sends one request message; the Message IDs of block-wise transfers are BL-4471's |
| Coap:RFC7252-4.5-1 | SHOULD | A recipient acknowledges each duplicate copy of a Confirmable message. | n/a: curl ends the exchange and closes its endpoint once it takes the response, so no later copy reaches it |
| Coap:RFC7252-4.5-2 | SHOULD | A recipient processes the response in duplicated messages only once. | CoapConfirmableExchangeTests.DuplicatePiggybackedResponse_IsWrittenOnce |
| Coap:RFC7252-4.6-1 | SHOULD | A message fits in a single IP packet; without a known Path MTU, 1280 bytes are assumed (1152 for message and payload). | CoapMessageFormatTests.Get_RequestHeader_IsVersion1ConfirmableGetWithTokenAndUriOptions |
| Coap:RFC7252-4.7-1 | MUST | A client strictly limits its outstanding interactions with a server to NSTART (1). | CoapRetransmissionTests.SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28 |
| Coap:RFC7252-4.8-1 | MUST NOT | ACK_RANDOM_FACTOR is not decreased below 1.0. | CoapRetransmissionTests.SilentServer_RequestIsRetransmittedFourTimesAtDoublingTimeoutsThenExit28 |

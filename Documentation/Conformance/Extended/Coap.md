# CoAP requirement matrix (RFC 7252)

Curl's `coap://` client (`Curl.Extended.Protocol.Coap.UnitLibrary`) measured against RFC 7252 by the cases in
`Curl.Conformance.Extended.UnitTests/Coap/`, run against `CoapReferenceServer`
(`Curl.Conformance.Extended.UnitLibrary/Coap/`). BL-4524 holds sections 3 (message format) and 4 (messaging), the
requirements that bind a client; BL-4525 adds section 3.1's extended option encodings and section 5
(request/response semantics, `Coap/RequestResponse/`); BL-4526 adds section 5.9 (response codes as curl reports them, with and without `-f` and `-i`), section 6 (`coap` URIs
and the Uri-* options, `Coap/UriAndResponse/`) and section 12 (the registries a client uses), which completes the
matrix for RFC 7252. RFC 7959 and RFC 7641 are in [Coap-BlockwiseObserve.md](Coap-BlockwiseObserve.md) and `coaps://`
(RFC 7252 section 9) in [Coap-Dtls-Handshake.md](Coap-Dtls-Handshake.md). Where the
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
| Coap:RFC7252-3.1-3 | (unkeyworded) | An Option Delta of 13 to 268 is written as nibble 13 and one extended byte holding the delta minus 13. | CoapOptionEncodingTests.UriQueryAsTheFirstOption_IsSentWithTheExtended13Delta |
| Coap:RFC7252-3.1-4 | (unkeyworded) | An Option Delta of 269 or more is written as nibble 14 and two extended bytes holding the delta minus 269 (read in a response; curl sends no option numbered 269 or above). | CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedElectiveOptionsInExtendedEncodings_IsWrittenAndAcknowledged |
| Coap:RFC7252-3.1-5 | (unkeyworded) | An Option Length of 13 to 268 is written as nibble 13 and one extended byte holding the length minus 13. | CoapOptionEncodingTests.LongUriPathSegments_AreSentInOptionOrderWithTheExtendedLengthEncodings |
| Coap:RFC7252-3.1-6 | (unkeyworded) | An Option Length of 269 or more is written as nibble 14 and two extended bytes holding the length minus 269, in a request and in a response. | CoapOptionEncodingTests.LongUriPathSegments_AreSentInOptionOrderWithTheExtendedLengthEncodings |
| Coap:RFC7252-3.1-7 | MUST | Options appear in order of their Option Numbers, each as a delta from the one before. | CoapOptionEncodingTests.LongUriPathSegments_AreSentInOptionOrderWithTheExtendedLengthEncodings |
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
| Coap:RFC7252-5.2.2-1 | MUST | A separate response that arrives in a Confirmable message is acknowledged by the client. | CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedElectiveOptionsInExtendedEncodings_IsWrittenAndAcknowledged |
| Coap:RFC7252-5.3.1-1 | SHOULD | A client generates tokens so that those in use for an endpoint pair are unique. | CoapTokenMatchingTests.Tokens_HoldAtLeast32BitsAndDifferBetweenRequests |
| Coap:RFC7252-5.3.1-2 | SHOULD | A client connected to the general Internet uses at least 32 bits of randomness in its tokens. | CoapTokenMatchingTests.Tokens_HoldAtLeast32BitsAndDifferBetweenRequests |
| Coap:RFC7252-5.3.2-1 | MUST | A piggybacked response matches both the Message ID and the token of the request; one whose token does not match is rejected (ignored). | CoapTokenMatchingTests.PiggybackedResponseWithAMismatchedToken_IsIgnored |
| Coap:RFC7252-5.3.2-2 | MUST | A separate response matches the token of the request; a Confirmable one whose token does not match is rejected with a Reset. | CoapTokenMatchingTests.SeparateResponseWithAMismatchedToken_IsRejectedWithAReset |
| Coap:RFC7252-5.3.2-3 | MUST | The source endpoint of a response is the destination endpoint of the request. | CoapTokenMatchingTests.ResponseFromAnotherEndpoint_IsIgnored |
| Coap:RFC7252-5.4.1-1 | MUST | An unrecognised critical option in a Confirmable response, or piggybacked in an Acknowledgement, rejects the response. | CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedCriticalOption_IsRejectedWithAReset |
| Coap:RFC7252-5.4.1-2 | MUST | An unrecognised critical option in a Non-confirmable message rejects the message. | CoapOptionHandlingTests.NonConfirmableResponseWithUnrecognisedCriticalOption_IsRejected |
| Coap:RFC7252-5.4.1-3 | MUST | An unrecognised elective option is silently ignored. | CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedElectiveOptionsInExtendedEncodings_IsWrittenAndAcknowledged |
| Coap:RFC7252-5.4.1-4 | MUST | An unrecognised critical option in a Confirmable request is answered with 4.02 (Bad Option). | n/a: binds the server that receives the request; curl is the client |
| Coap:RFC7252-5.4.2-1 | MUST | A proxy handles an unrecognised unsafe option as Proxy Unsafe and does not forward it blindly. | n/a: binds a proxy; curl's coap:// client is no CoAP proxy |
| Coap:RFC7252-5.4.3-1 | MUST | An option whose value length is outside the option's defined range is treated like an unrecognised option. | CoapOptionHandlingTests.PiggybackedResponseWithSupernumeraryAndOutOfRangeElectiveOptions_IsWritten |
| Coap:RFC7252-5.4.5-1 | MUST | Each occurrence of a non-repeatable option beyond the first is treated like an unrecognised option. | CoapOptionHandlingTests.PiggybackedResponseWithSupernumeraryAndOutOfRangeElectiveOptions_IsWritten |
| Coap:RFC7252-5.5-1 | (unkeyworded) | The payload of a request (POST, PUT) carries the representation sent; the payload of a response carries the representation returned. | CoapRequestMethodTests.Method_IsSentWithItsCodeAndPayloadAndTheResponsePayloadIsWritten |
| Coap:RFC7252-5.5-2 | (unkeyworded) | A response's Content-Format option names its payload's media type; the payload is passed on as it is. | CoapOptionHandlingTests.ConfirmableResponseWithUnrecognisedElectiveOptionsInExtendedEncodings_IsWrittenAndAcknowledged |
| Coap:RFC7252-5.6-1 | MUST, MUST NOT | A cached response is used only while fresh, and revalidated through ETag. | n/a: curl caches no CoAP response; each run sends its request |
| Coap:RFC7252-5.7-1 | MUST | The forward- and reverse-proxy rules of section 5.7. | n/a: binds a proxy; curl sends no Proxy-Uri or Proxy-Scheme option |
| Coap:RFC7252-5.8-1 | (unkeyworded) | GET, POST, PUT and DELETE are request codes 0.01, 0.02, 0.03 and 0.04. | CoapRequestMethodTests.Method_IsSentWithItsCodeAndPayloadAndTheResponsePayloadIsWritten |
| Coap:RFC7252-5.10-1 | MUST, MUST NOT | The rules for Proxy-Uri, Accept, ETag, If-Match and If-None-Match in a request (section 5.10). | n/a: curl sends none of these options in a coap:// request; Uri-Host, Uri-Port, Uri-Path and Uri-Query (sections 5.10.1 and 6.4) are BL-4526's |
| Coap:RFC7252-5.9-1 | (unkeyworded) | Class 2 response codes say the request succeeded; class 4 (client error) and class 5 (server error) say it failed. curl writes a 4.xx or 5.xx payload with exit 0 as it does an HTTP error status, and ends with exit 22 under `-f`. | CoapResponseCodeTests.ErrorResponse_WithFail_EndsWithExit22AndWritesNothing |
| Coap:RFC7252-5.9-2 | (unkeyworded) | A response code the client does not recognise is treated as the generic code of its class (4.00 or 5.00). | CoapResponseCodeTests.ErrorResponse_WithFail_EndsWithExit22AndWritesNothing |
| Coap:RFC7252-5.9-3 | SHOULD | A 4.xx or 5.xx response carries a diagnostic payload. | n/a: binds the server that answers; curl writes whatever payload the response carries (CoapResponseCodeTests.ErrorResponse_WithoutFail_IsWrittenWithExit0) |
| Coap:RFC7252-6.1-1 | (unkeyworded) | A `coap` URI without a port, or with an empty one, means UDP port 5683. | CoapUriOptionTests.Port_IsTheDestinationAndNoUriPortIsSent |
| Coap:RFC7252-6.2-1 | MUST, MUST NOT | The `coaps` URI scheme, its default port 5684 and DTLS. | n/a: `coaps://` is BL-4472's, see [Coap-Dtls-Handshake.md](Coap-Dtls-Handshake.md) |
| Coap:RFC7252-6.3-1 | (unkeyworded) | Normalisation and comparison of `coap` URIs (case of scheme and host, default port, percent-encoding). | CoapUriOptionTests.HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded |
| Coap:RFC7252-6.4-1 | (unkeyworded) | Unless the host is an IP literal, Uri-Host is the host, lowercased and percent-decoded. | CoapUriOptionTests.HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded |
| Coap:RFC7252-6.4-2 | (unkeyworded) | Uri-Port is included only when the URI's port differs from the request's destination UDP port; the default port is never sent. | CoapUriOptionTests.Port_IsTheDestinationAndNoUriPortIsSent |
| Coap:RFC7252-6.4-3 | (unkeyworded) | Each path segment, empty segments included, is one Uri-Path option, percent-decoded (an encoded `/` stays inside its segment). | CoapUriOptionTests.HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded |
| Coap:RFC7252-6.4-4 | (unkeyworded) | Each `&`-separated query argument is one Uri-Query option, percent-decoded. | CoapUriOptionTests.HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded |
| Coap:RFC7252-6.4-5 | (unkeyworded) | An IP-literal host sends no Uri-Host; a path that is empty or `/` sends no Uri-Path. | CoapUriOptionTests.IpLiteralHostAndEmptyPath_SendNoUriHostAndNoUriPath |
| Coap:RFC7252-6.4-6 | (unkeyworded) | A URI's fragment is never decomposed into an option. | CoapUriOptionTests.Fragment_IsNotSent |
| Coap:RFC7252-6.5-1 | (unkeyworded) | Composing a URI from the Uri-* options of a received request. | n/a: binds the server or proxy that receives the request; curl is the client |
| Coap:RFC7252-12.1-1 | (unkeyworded) | The response code registry: 4.00 to 4.15 and 5.00 to 5.05 as the client error and server error codes. | CoapResponseCodeTests.ErrorResponse_WithoutFail_IsWrittenWithExit0 |
| Coap:RFC7252-12.1-2 | (unkeyworded) | A response code is written `c.dd` (class, then two-digit detail); curl's `-i` writes it as the status line `CoAP/1.0 c.dd`. | CoapResponseCodeTests.IncludeHeaders_WritesTheResponseCodeAsAStatusLineBeforeThePayload |
| Coap:RFC7252-12.2-1 | (unkeyworded) | The option number registry: Uri-Host 3, Uri-Port 7, Uri-Path 11, Uri-Query 15. | CoapUriOptionTests.HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded |
| Coap:RFC7252-12.3-1 | (unkeyworded) | The Content-Format registry. | n/a: curl sends no Content-Format and passes a response's payload on as it is whatever its Content-Format (Coap:RFC7252-5.5-2) |
| Coap:RFC7252-12.6-1 | (unkeyworded) | The `coap` URI scheme registration, default port 5683. | CoapUriOptionTests.Port_IsTheDestinationAndNoUriPortIsSent |

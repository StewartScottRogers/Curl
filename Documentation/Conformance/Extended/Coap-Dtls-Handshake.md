# CoAP requirement matrix (DTLS 1.2 handshake, RFC 6347 and RFC 7252 section 9.1)

Curl's `coaps://` client (`Curl.Extended.Protocol.Coap.UnitLibrary`, `Dtls/` and `Secure/DtlsDatagramChannel`)
measured against RFC 6347 and RFC 7252 section 9.1 by the cases in `Curl.Conformance.Extended.UnitTests/Coap/Dtls/`,
run against `DtlsReferenceServer` in `Curl.Conformance.Extended.UnitLibrary/Coap/Dtls/`: a DTLS 1.2 server written
from RFC 6347 and RFC 5246 that answers a ClientHello without its cookie with a HelloVerifyRequest, completes a full
handshake under TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 with a self-signed RSA certificate, and passes each protected
application data record to `CoapReferenceServer`. This part (BL-4573) holds the cookie exchange, the epochs, the
certificate check and the protected exchange; the server's fault hooks (fragmenting, reordering, dropping,
replaying, alerting) and their requirements follow in BL-4574 (`Coap-Dtls-Faults.md`) and BL-4575 (`Coap-Dtls-Remaining.md`). A row whose case fails is listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with its fix task.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Coap:RFC6347-4.1-1 | MUST | The epoch is incremented after each ChangeCipherSpec, and the sequence number starts again at 0 in the new epoch: the client's Finished is record 0 of epoch 1. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC6347-4.2.1-1 | MUST | A client answers a HelloVerifyRequest with its ClientHello again, carrying the cookie the server sent. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC7252-9.1-1 | (unkeyworded) | A `coaps` URI without a port uses the default port 5684. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC7252-9.1-2 | MUST | In certificate mode the server's certificate is checked; one the client does not trust ends the handshake with exit 60 before any key exchange. | CoapDtlsHandshakeTests.UntrustedCertificate_FailsWithExit60BeforeAnyKeyExchange |
| Coap:RFC7252-9.1-3 | (unkeyworded) | Under `-k` the certificate check is waived and the handshake completes. | CoapDtlsHandshakeTests.UntrustedCertificateUnderInsecure_TheHandshakeCompletesAndTheResponseIsWritten |
| Coap:RFC7252-9.1-4 | (unkeyworded) | CoAP messages go in DTLS application data records, one message per record, never in the clear. | CoapDtlsProtectedExchangeTests.Get_TheRequestAndResponseAreEachOneProtectedRecordAndNeverCrossInTheClear |

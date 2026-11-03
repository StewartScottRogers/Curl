# CoAP requirement matrix (DTLS 1.2, the rest of RFC 6347, RFC 5246 and RFC 7252 section 9.1)

Curl's `coaps://` client (`Curl.Extended.Protocol.Coap.UnitLibrary`, `Dtls/` and `Secure/DtlsDatagramChannel`)
measured against every client requirement of RFC 6347, the RFC 5246 handshake it inherits and RFC 7252 section 9.1
that `Coap-Dtls-Handshake.md` (BL-4573) and `Coap-Dtls-Faults.md` (BL-4574) do not already hold (BL-4575). The cases
are in `Curl.Conformance.Extended.UnitTests/Coap/Dtls/` and run against `DtlsReferenceServer` in
`Curl.Conformance.Extended.UnitLibrary/Coap/Dtls/`, here with `DtlsReferenceCorruptRecordFault` (a record whose
authentication tag is wrong before the real answer) and `DtlsReferenceWrongFinishedFault` (a server Finished whose
verify_data is wrong). A requirement that binds only a server, or a feature curl's `coaps://` client does not
offer, is `n/a` with its reason. A row whose case fails is listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with its fix task.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Coap:RFC6347-4.2.1-2 | MUST | The second ClientHello uses the same version, random, session_id, cipher_suites and compression_method as the first, adding only the cookie. | CoapDtlsClientHelloTests.SecondClientHello_RepeatsTheFirstsVersionRandomSessionCipherSuitesAndCompressionAndAddsOnlyTheCookie |
| Coap:RFC6347-4.2.1-3 | (unkeyworded) | A DTLS 1.2 ClientHello carries client_version {254, 253}. | CoapDtlsClientHelloTests.ClientHello_OffersDtls12TheServersCipherSuiteAndTheNullCompressionMethod |
| Coap:RFC5246-7.4.1.2-1 | MUST | The ClientHello's compression_methods include the null method (0). | CoapDtlsClientHelloTests.ClientHello_OffersDtls12TheServersCipherSuiteAndTheNullCompressionMethod |
| Coap:RFC6347-4.1.2.7-1 | SHOULD | A record that fails authentication is discarded silently, without an alert, and the association goes on. | CoapDtlsInvalidRecordTests.RecordThatFailsAuthenticationBeforeTheAnswer_CurlDropsItSilentlyAndWritesTheRealAnswer |
| Coap:RFC5246-7.4.9-1 | MUST | The recipient of a Finished verifies its verify_data; a wrong one ends the handshake, and no application data is sent. | CoapDtlsFinishedTests.ServerFinishedWithWrongVerifyData_CurlStopsWithExit35AndSendsNoRequest |
| Coap:RFC6347-4.1-4 | MUST | A record's length field gives the length of its fragment; several records may share one datagram, none crosses two. | n/a: a framing rule both sides apply; every case of Coap-Dtls-Handshake.md and Coap-Dtls-Faults.md exchanges records through it, and a server sends none that cross datagrams |
| Coap:RFC6347-4.1.1-1 | SHOULD | Records are sized to fit the path MTU; a handshake message larger than it is fragmented. | n/a: curl's ClientHello and ClientKeyExchange fit any IPv4 minimum MTU, so the client never has a message to fragment |
| Coap:RFC6347-4.1.1.1-1 | SHOULD | An implementation that receives an ICMP "datagram too big" lowers its record size. | n/a: the harness's datagram route has no ICMP, and curl's flights fit the minimum MTU |
| Coap:RFC6347-4.1.2.1-1 | MUST | The MAC of a record covers its epoch and sequence number. | n/a: under the AEAD cipher suites curl offers the epoch and sequence number are in the additional data; every protected record a case exchanges proves it, since the reference server opens it with them |
| Coap:RFC6347-4.1.2.3-1 | MUST NOT | Stream ciphers are not used in DTLS. | n/a: curl offers only AES-GCM and AES-CCM8 cipher suites |
| Coap:RFC6347-4.1.2.6-2 | MUST | The replay window advances only for a record that authenticates. | n/a: a corrupt record before the answer (Coap:RFC6347-4.1.2.7-1) and a replayed one (Coap:RFC6347-4.1.2.6-1) cover both sides; the window's advance is not visible on the wire apart from them |
| Coap:RFC6347-4.1.2.7-2 | MUST | A client that sends an alert for an invalid record makes it fatal. | n/a: curl discards an invalid record silently (Coap:RFC6347-4.1.2.7-1) and sends no alert for it |
| Coap:RFC6347-4.1.2.8-1 | MUST | A record for a new connection from the same address and port is handled. | n/a: binds a server; curl's client opens one association per transfer |
| Coap:RFC6347-4.2.1-4 | MUST | A server that receives a ClientHello with a valid cookie proceeds with the handshake. | n/a: binds a server |
| Coap:RFC6347-4.2.1-5 | MUST | The HelloVerifyRequest's server_version is {254, 255} whatever version is negotiated. | n/a: binds a server; the reference server sends it so (DtlsReferenceServer.AnswerClientHello) |
| Coap:RFC6347-4.2.1-6 | MUST | A client that does not get a HelloVerifyRequest is prepared to take a ServerHello at once. | n/a: the reference server always sends a HelloVerifyRequest; a server that skips it is a configuration no case needs to exercise beyond the handshake curl completes with the cookie |
| Coap:RFC6347-4.2.2-2 | MUST | message_seq starts at 0 and goes up by one for each new handshake message, and a retransmission keeps its message_seq. | CoapDtlsClientHelloTests.PlainHandshakeMessages_AreNumberedFrom0AndChangeCipherSpecIsTheUnnumberedByte1 |
| Coap:RFC6347-4.2.3-2 | MUST | A message too large for one record is fragmented into several, each carrying fragment_offset and fragment_length. | n/a: curl's handshake messages fit one record (Coap:RFC6347-4.1.1-1); reassembly of the server's fragments is Coap:RFC6347-4.2.3-1 |
| Coap:RFC6347-4.2.4-2 | SHOULD | The retransmission timer starts at 1 second and doubles on each retransmission, up to 60 seconds. | n/a: the timer's values are not visible on the wire; the harness's clock jumps to the next timer whatever its due time, so Coap:RFC6347-4.2.4-1 pins that the timer fires and the flight goes again, not after how long |
| Coap:RFC6347-4.2.4-3 | MUST | A server's retransmitted last flight is answered by retransmitting the client's flight. | n/a: binds the side that sends the last flight; for a full handshake that is the server |
| Coap:RFC6347-4.2.5-1 | MUST | ChangeCipherSpec is the single byte 1 and takes no message_seq, so the handshake messages around it are numbered without a gap. | CoapDtlsClientHelloTests.PlainHandshakeMessages_AreNumberedFrom0AndChangeCipherSpecIsTheUnnumberedByte1 |
| Coap:RFC6347-4.2.6-1 | MUST | The Finished's verify_data covers every handshake message with its DTLS header, the first ClientHello and HelloVerifyRequest excluded. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC6347-4.2.8-1 | MUST | Messages of a new handshake on an established association are carried under the next epoch. | n/a: curl's `coaps://` client never renegotiates; one handshake per transfer |
| Coap:RFC6347-4.3-1 | MUST | Applications choose cipher suites that do not rely on stream ciphers or on TLS 1.0 behaviour. | n/a: curl offers only the AEAD suites 0xC02B, 0xC02F and 0xC0AE (Coap:RFC6347-4.1.2.3-1) |
| Coap:RFC6347-4.2.4.1-1 | SHOULD | An application uses the PMTU as its record size and handles a lower MTU by backing off. | n/a: as Coap:RFC6347-4.1.1.1-1, no ICMP on the route and curl's flights fit the minimum MTU |
| Coap:RFC5246-7.4.1.3-1 | MUST | A client offered a cipher suite or version it did not offer ends the handshake with a fatal alert. | n/a: the reference server chooses only what curl offers; exercising the client's refusal needs a server fault this matrix does not yet have |
| Coap:RFC5246-7.4.2-1 | MUST | The server's certificate chain is validated in certificate mode. | n/a: Coap:RFC7252-9.1-2 holds it for coaps:// |
| Coap:RFC5246-7.4.3-1 | MUST | The ServerKeyExchange signature over both randoms and the parameters is verified. | CoapDtlsServerKeyExchangeTests.ServerKeyExchangeWithWrongSignature_CurlStopsWithExit35BeforeItsClientKeyExchange |
| Coap:RFC5246-7.4.4-1 | MUST | A client asked for a certificate it does not have sends an empty Certificate. | n/a: the reference server never sends a CertificateRequest; curl's `coaps://` client sends no client certificate |
| Coap:RFC5246-7.4.7-1 | MUST | The ClientKeyExchange carries the client's ECDHE public value on the server's curve. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC5246-7.4.9-2 | MUST | A party sends its own Finished only after its ChangeCipherSpec, under the new keys. | CoapDtlsHandshakeTests.FullHandshake_TheSecondClientHelloEchoesTheCookieAndFinishedIsTheFirstRecordOfEpoch1 |
| Coap:RFC5246-7.2.1-1 | MUST | A party that sends close_notify, or receives one, ends the connection. | n/a: one CoAP exchange per association; curl closes the socket when the transfer ends, and RFC 7252 section 9.1 allows it to keep or drop the association |
| Coap:RFC5246-7.2.2-1 | MUST | A party that receives a fatal alert does not resume the session. | n/a: curl's `coaps://` client never resumes sessions |
| Coap:RFC5246-7.4.1.4-1 | MUST NOT | An extension type is not repeated in a hello. | n/a: curl's ClientHello extension block is fixed; Coap:RFC6347-4.2.1-2 pins it identical between the two ClientHellos |
| Coap:RFC7252-9.1.3.3-1 | MUST | An implementation in certificate (X.509) mode supports TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8. | n/a: the reference server signs with RSA and picks 0xC02F; the CCM_8 suite is offered and its record cipher is pinned by the library's own unit tests |
| Coap:RFC7252-9.1.3.1-1 | MUST | In PreSharedKey mode TLS_PSK_WITH_AES_128_CCM_8 is supported. | n/a: curl's `coaps://` client offers certificate mode only, with ECDHE suites and no PSK or raw public key (ADR-1078) |
| Coap:RFC7252-9.1.3.2-1 | MUST | In RawPublicKey mode TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 is supported with raw public keys (RFC 7250). | n/a: curl's `coaps://` client offers certificate mode only, with ECDHE suites and no PSK or raw public key (ADR-1078) |
| Coap:RFC7252-9.1.3.3-2 | SHOULD | The client checks the server's identity against the URI's host, as RFC 6125 says. | n/a: Coap:RFC7252-9.1-2 holds the certificate check, host name included, through the platform's chain check |
| Coap:RFC7252-9.1.2-1 | MUST | A response is matched to its request only in the same DTLS session and epoch. | n/a: curl sends one request per association and never changes epoch after the handshake; a record from another epoch is dropped (Coap:RFC6347-4.1-2) |
| Coap:RFC7252-9.1.1-1 | (unkeyworded) | Messaging-layer retransmission and deduplication of a CoAP message stay within one DTLS session and epoch. | n/a: the reference CoAP server answers at once, so no CoAP retransmission is due over DTLS; CoAP retransmission is pinned in the clear by Coap.md |

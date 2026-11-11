# Xmpp: RFC 6120 streams, STARTTLS, SASL and binding, RFC 6121 messages and XEP-0030 disco

Curl's `xmpp://` and `xmpps://` client (ADR-1068) measured against RFC 6120 (4 streams, 5 STARTTLS, 6 SASL, 7 resource
binding, 8 stanzas, 11 XML usage), the SASL mechanisms PLAIN (RFC 4616), ANONYMOUS (RFC 4505), SCRAM-SHA-1 (RFC 5802)
and SCRAM-SHA-256 (RFC 7677), RFC 6121's message stanza and XEP-0030's disco query, and XEP-0368's direct TLS for
`xmpps://`. IDs are `Xmpp:<source>-<section>-<n>`. The cases run against `XmppReferenceServer`
(`Curl.Conformance.Extended.UnitLibrary/Xmpp/`), which reads curl's stream with the BCL XML reader, upgrades to TLS
after `<proceed/>`, checks SCRAM client proofs and computes server signatures itself, binds a server-assigned resource
and sends the answer the case scripts. Server-only requirements are `n/a`.

`--ssl-reqd` follows curl's rule for its STARTTLS protocols (smtp, imap, pop3, ftp) and RFC 6120 13.8: on `xmpp://`,
when the server's first `<stream:features/>` offers no `<starttls/>`, the transfer ends with `curl: (64) Requested SSL
level failed` before any `<auth/>`, so no credential crosses a plaintext connection. Without it, and under `--ssl`,
the client logs in in plaintext when STARTTLS is not offered; `xmpps://` is secure from the start. The unit tests in
`Curl.Extended.Protocol.Xmpp.UnitTests/Client/XmppSslRequiredTests.cs` pin both paths.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Xmpp:RFC6120-4.1-1 | MUST | A stream is read as XML however its bytes arrive across reads. | XmppStreamCases.SplitStanza_DiscoAnswerAcrossThreeReads_IsReadWhole |
| Xmpp:RFC6120-4.2-1 | MUST | The initiating entity opens the stream by sending a stream header. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.4-1 | MUST | A party closing the stream sends `</stream:stream>`. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.7.2-1 | SHOULD | The initiating entity includes `to` set to the receiving entity's domain. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.7.3-1 | MUST NOT | The initiating entity sends no stream `id`. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.7.5-1 | MUST | The stream header carries `version='1.0'`. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.8.2-1 | MUST | The content namespace is `jabber:client` and the stream namespace `http://etherx.jabber.org/streams`. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-4.9.1.1-1 | MUST | A stream error is unrecoverable: the client stops and reports its condition. | XmppStreamCases.StreamError_ServerSendsConflict_FailsWithWeirdServerReplyAndTheCondition |
| Xmpp:RFC6120-4.9.1.2-1 | MUST | The receiving entity closes the stream after a stream error. | n/a: binds the receiving entity |
| Xmpp:RFC6120-5.3.1-1 | MUST | When STARTTLS is offered as required, the client negotiates TLS before anything else. | XmppStreamCases.StartTls_OfferedAsRequired_IsNegotiatedAndTheStreamRestartsBehindTls |
| Xmpp:RFC6120-5.3.1-2 | SHOULD | The client negotiates TLS when the server offers it as optional. | XmppStreamCases.StartTls_OfferedAsOptional_IsStillNegotiated |
| Xmpp:RFC6120-5.4.2.1-1 | MUST | The client sends `<starttls/>` and waits for `<proceed/>` before the TLS handshake. | XmppStreamCases.StartTls_OfferedAsRequired_IsNegotiatedAndTheStreamRestartsBehindTls |
| Xmpp:RFC6120-5.4.3.3-1 | MUST | After TLS the client opens a new stream, without closing the old one. | XmppStreamCases.StartTls_OfferedAsRequired_IsNegotiatedAndTheStreamRestartsBehindTls |
| Xmpp:RFC6120-5.4.3.2-1 | MUST | The receiving entity presents its certificate and features again after TLS. | n/a: binds the receiving entity |
| Xmpp:RFC6120-6.3.10-1 | MUST | The client verifies the additional data that comes with `<success/>`. | XmppSaslCases.ScramSha256_OfferedLastByTheServer_IsChosenAndItsProofVerifies |
| Xmpp:RFC6120-6.4.1-1 | MUST | The receiving entity offers its mechanisms in `<mechanisms/>`. | n/a: binds the receiving entity |
| Xmpp:RFC6120-6.4.2-1 | MUST | The client sends `<auth/>` naming a mechanism the server offered, chosen by the client's own preference. | XmppSaslCases.ScramSha256_OfferedLastByTheServer_IsChosenAndItsProofVerifies |
| Xmpp:RFC6120-6.4.2-2 | MUST | An empty initial response is sent as `=`. | XmppSaslCases.Anonymous_WithoutUser_SendsAnEmptyInitialResponseAsEquals |
| Xmpp:RFC6120-6.4.6-1 | MUST | After `<success/>` the client opens a new stream. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6120-6.5-1 | MUST | `<failure/>` ends authentication; the client reports its defined condition. | XmppSaslCases.Failure_EachDefinedCondition_FailsWithLoginDeniedAndTheCondition |
| Xmpp:RFC4616-2-1 | MUST | PLAIN's message is authzid NUL authcid NUL password. | XmppSaslCases.Plain_OnlyMechanismOffered_SendsEmptyAuthzidUserAndPassword |
| Xmpp:RFC4505-2-1 | MAY | ANONYMOUS sends empty or optional trace data. | XmppSaslCases.Anonymous_WithoutUser_SendsAnEmptyInitialResponseAsEquals |
| Xmpp:RFC5802-5-1 | MUST | SCRAM-SHA-1's client proof is computed from the salted password over the auth message. | XmppSaslCases.ScramSha1_OfferedWithPlain_IsChosenAndItsProofVerifies |
| Xmpp:RFC5802-3-1 | MUST | The client verifies the server signature and fails when it does not match. | XmppSaslCases.Scram_ServerSignatureForged_FailsWithLoginDenied |
| Xmpp:RFC7677-3-1 | MUST | SCRAM-SHA-256 is SCRAM over SHA-256 and is preferred to SCRAM-SHA-1. | XmppSaslCases.ScramSha256_OfferedLastByTheServer_IsChosenAndItsProofVerifies |
| Xmpp:RFC6120-7.6-1 | MUST | A client asking the server to generate its resource sends an empty `<bind/>` in an `iq` of type `set`. | XmppStanzaCases.Bind_ServerAssignsTheResource_RequestCarriesNoResource |
| Xmpp:RFC6120-7.6-2 | MUST | The server answers with the full JID it assigned. | n/a: binds the receiving entity |
| Xmpp:RFC6120-8.1.3-1 | MUST | An `iq` stanza carries an `id`. | XmppStanzaCases.Bind_ServerAssignsTheResource_RequestCarriesNoResource |
| Xmpp:RFC6120-8.1.1-1 | SHOULD | A message stanza carries `to`. | XmppStanzaCases.Message_FromData_IsAMessageStanzaToThePathJid |
| Xmpp:RFC6120-8.2.3-1 | MUST | An `iq` of type `get` gets exactly one `result` or `error`, matched by `id`. | XmppStanzaCases.Disco_InfoQuery_IsAnIqGetAndItsResultIsPrinted |
| Xmpp:RFC6120-8.3.1-1 | MUST | A stanza of type `error` carries `<error/>` with a defined condition; the client reports it. | XmppStanzaCases.StanzaError_DiscoAnsweredWithItemNotFound_FailsWithWeirdServerReplyAndTheCondition |
| Xmpp:RFC6120-11.1-1 | MUST NOT | The stream holds no DTD, comment or processing instruction other than the XML declaration. | XmppStreamCases.StreamHeader_PlainStream_OpensWithToAndVersionRestartsAfterSaslAndCloses |
| Xmpp:RFC6121-5.2-1 | MUST | A chat message's text goes in `<body/>`, escaped as XML character data. | XmppStanzaCases.Message_FromData_IsAMessageStanzaToThePathJid |
| Xmpp:XEP0030-3-1 | MUST | A disco#info query is an `iq` of type `get` holding an empty `query` in `http://jabber.org/protocol/disco#info`. | XmppStanzaCases.Disco_InfoQuery_IsAnIqGetAndItsResultIsPrinted |
| Xmpp:XEP0368-1 | MUST | Direct TLS opens the XML stream only after the TLS handshake, with no STARTTLS. | XmppStreamCases.Xmpps_DirectTls_OpensTheStreamBehindTlsWithoutStartTls |

`-T file` publishes like `-d`: the file's bytes become the message `<body/>` sent to the path JID (the upload stream is
read into memory first when `-d` gave no data). It never receives or consumes; `XmppUploadTests` pins it.

# Amqp1: protocol headers and SASL

Curl's `amqp1://` and `amqp1s://` client measured against OASIS AMQP Version 1.0 (29 October 2012): the version
negotiation and protocol headers of Part 2 section 2.2 and the SASL layer of Part 5 section 5.3. Transfer, flow,
disposition and the message format are measured by BL-4467; frame size, idle timeout, errors and encoding widths by
BL-4528 and BL-4529.

The cases run against `Amqp1ReferenceContainer` (`Curl.Conformance.Extended.UnitLibrary/Amqp1/`), which implements
the type system, framing, SASL and open, begin, attach, detach, end and close from the specification. Its default link
behaviour, `Amqp1ReferenceLinkRefusal`, refuses each link with a detach carrying amqp:not-found, so a run that gets
past attach ends with exit 78.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Amqp1:Core-2.2-1 | MUST | Prior to sending any frames on a connection, each peer starts by sending a protocol header for the version it uses. | Amqp1ProtocolHeaderCases.Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome |
| Amqp1:Core-2.2-2 | MUST | The protocol header is "AMQP", the protocol id, and major 1, minor 0, revision 0. | Amqp1ProtocolHeaderCases.Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome |
| Amqp1:Core-2.2-3 | MUST | The peer that opened the TCP connection sends its protocol header immediately, and data after a header matches the version that header names. | Amqp1ProtocolHeaderCases.Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome |
| Amqp1:Core-2.2-4 | MUST | When the incoming and outgoing protocol headers do not match, the peer closes its outgoing stream (sending no frame). | Amqp1ProtocolHeaderCases.Headers_ServerAnswersAnotherHeader_ClosesWithWeirdServerReply |
| Amqp1:Core-2.2-5 | SHOULD | After a header mismatch the peer reads the incoming stream until it is terminated. | n/a: the client closes the connection at once and leaves the server's close to the transport, a choice the specification leaves to the peer that cannot be observed in the bytes |
| Amqp1:Core-2.2-6 | MUST | A server that does not support the requested version answers with the header it supports and closes. | n/a: a server requirement |
| Amqp1:Sasl-5.3.1-1 | MUST | A client that uses SASL starts by sending the SASL protocol header, protocol id 3, before the AMQP header. | Amqp1ProtocolHeaderCases.Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome |
| Amqp1:Sasl-5.3.1-2 | MUST | SASL frames are frame type 0x01 and are the only frames exchanged before the outcome. | n/a: the frame type the client writes is pinned by Amqp1ProtocolHeaderCases' sasl-init assertion, which carries 5.3.1-1 |
| Amqp1:Sasl-5.3.2-1 | MUST | The client chooses one of the mechanisms sasl-mechanisms offers and starts the exchange with sasl-init. | Amqp1SaslCases.Sasl_UserGiven_InitsPlainWithItsCredentials |
| Amqp1:Sasl-5.3.2-2 | MUST NOT | The client does not send sasl-init for a mechanism the server did not offer. | Amqp1SaslCases.Sasl_MechanismNotOffered_FailsWithLoginDeniedBeforeInit |
| Amqp1:Sasl-5.3.3.2-1 | MUST | sasl-init's initial-response carries the mechanism's first message: for PLAIN, [authzid] NUL authcid NUL passwd. | Amqp1SaslCases.Sasl_UserGiven_InitsPlainWithItsCredentials |
| Amqp1:Sasl-5.3.3.2-2 | MUST | Without credentials the client uses a mechanism that needs none, ANONYMOUS. | Amqp1SaslCases.Sasl_NoUser_InitsAnonymous |
| Amqp1:Sasl-5.3.3.4-1 | MUST | The client answers each sasl-challenge with a sasl-response. | n/a: the client uses only PLAIN and ANONYMOUS, which have no challenge |
| Amqp1:Sasl-5.3.3.6-1 | MUST | After an ok outcome both peers exchange protocol headers to establish the AMQP connection. | Amqp1ProtocolHeaderCases.Headers_Amqp1_SaslHeaderFirstThenAmqpHeaderAfterOutcome |
| Amqp1:Sasl-5.3.3.6-2 | MUST NOT | After an outcome other than ok the client does not go on to the AMQP connection. | Amqp1SaslCases.Sasl_OutcomeAuth_FailsWithLoginDeniedAndNoAmqpHeader |

# Amqp1: protocol headers, SASL, connection, session and link

Curl's `amqp1://` and `amqp1s://` client measured against OASIS AMQP Version 1.0 (29 October 2012): the version
negotiation and protocol headers of Part 2 section 2.2, the SASL layer of Part 5 section 5.3, and the framing,
connection, session, link attach and detach, and error requirements of Part 2 sections 2.3 to 2.6 and 2.8 (BL-4528).
Transfer, flow, disposition and the message format are measured by BL-4467; encoding widths by BL-4529.

The cases run against `Amqp1ReferenceContainer` (`Curl.Conformance.Extended.UnitLibrary/Amqp1/`), which implements
the type system, framing, SASL and open, begin, attach, detach, end and close from the specification. Its default link
behaviour, `Amqp1ReferenceLinkRefusal`, refuses each link with a detach carrying amqp:not-found, so a run that gets
past attach ends with exit 78. The connection and link cases instead use their own link behaviours, written in the case files: one
that grants credit and accepts each delivery (optionally after empty frames, sent with `Amqp1ReferenceSession.SendEmptyFrameAsync`, or a delay), and one that closes the
connection with an error.

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
| Amqp1:Core-2.3.1-1 | MUST | A frame's SIZE is the total frame size, header included, and is at least the 8-byte header. | Amqp1ConnectionCases.FrameSize_ContainerOffers512_EveryFrameFitsAndTheMessageIsSplit |
| Amqp1:Core-2.3.1-2 | MUST | A frame's DOFF, the data offset in 4-byte words, is at least 2. | Amqp1ConnectionCases.FrameSize_ContainerOffers512_EveryFrameFitsAndTheMessageIsSplit |
| Amqp1:Core-2.3.2-1 | MUST | An AMQP frame's channel names the session it belongs to, the channel its begin was sent on. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.3.2-2 | MUST | An AMQP frame with no body is accepted and ignored: it only keeps the connection from idling out. | Amqp1ConnectionCases.EmptyFrames_ContainerSendsTwoBeforeFlow_CurlIgnoresThem |
| Amqp1:Core-2.4.1-1 | MUST | After the AMQP header, the first frame each peer sends is open. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.4.1-2 | MUST NOT | A peer sends no frame larger than the max-frame-size the other peer's open gives. | Amqp1ConnectionCases.FrameSize_ContainerOffers512_EveryFrameFitsAndTheMessageIsSplit |
| Amqp1:Core-2.4.1-3 | MUST | The max-frame-size a peer's open offers is at least 512 (MIN-MAX-FRAME-SIZE). | Amqp1ConnectionCases.FrameSize_ContainerOffers512_EveryFrameFitsAndTheMessageIsSplit |
| Amqp1:Core-2.4.1-4 | MUST NOT | A peer sends no frame other than open before its open. | n/a: carried by Amqp1:Core-2.4.1-1, whose case pins open as curl's first AMQP frame |
| Amqp1:Core-2.4.3-1 | MUST | Before closing a connection a peer writes close, and close is the last frame it writes. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.4.3-2 | MUST | A peer that receives close answers it with its own close before ending the connection. | Amqp1ConnectionCases.CloseWithError_ContainerClosesAfterAttach_CurlAnswersWithClose |
| Amqp1:Core-2.4.3-3 | SHOULD | After writing close a peer reads until the other peer's close arrives. | n/a: curl ends when it reads the container's answering close, which the order case's exit 0 shows; that it would keep reading longer cannot be observed in the bytes |
| Amqp1:Core-2.4.5-1 | MUST | When the other peer's open gives an idle-time-out, a peer sends frames, empty ones if it has nothing else, often enough that the timeout never expires. | Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames |
| Amqp1:Core-2.4.5-2 | SHOULD | A peer advertises as its idle-time-out half of the threshold at which it closes an idle connection. | n/a: curl advertises no idle-time-out and closes no connection for idling |
| Amqp1:Core-2.4.5-3 | MUST | A peer whose idle-time-out expires closes the connection with an error. | n/a: curl advertises no idle-time-out, so it has none to expire; -m bounds the whole transfer instead |
| Amqp1:Core-2.5.1-1 | MUST | A session begins with begin on an unused channel; the begin of the peer that starts the session carries no remote-channel. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.5.1-2 | MUST | A session is ended with end before the connection is closed, and nothing more is sent on its channel after end. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.5.6-1 | MUST NOT | A peer sends no more transfers than the other peer's session incoming-window allows. | n/a: measured with transfer and flow by BL-4467 |
| Amqp1:Core-2.6.3-1 | MUST | A link is attached with attach, carrying its name, the handle later frames use and its role, before any frame on that handle. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.6.3-2 | MUST NOT | After the other peer refuses a link (a detach with an error after its attach), a peer sends no transfer on it. | Amqp1SessionLinkCases.Link_RefusedByDetachWithError_SendsNoTransferAndReportsTheError |
| Amqp1:Core-2.6.5-1 | MUST | A peer whose link the other peer detaches with closed set answers with its own closing detach. | Amqp1SessionLinkCases.Link_DetachedByThePeer_AnswersWithADetach |
| Amqp1:Core-2.6.6-1 | MUST | A link is closed with a detach whose closed field is true, on the attached handle, before the session ends. | Amqp1SessionLinkCases.Order_SendMessage_OpenBeginAttachTransferDetachEndClose |
| Amqp1:Core-2.8.15-1 | MUST | An error carries a condition symbol and may carry a description; detach, end and close carry the error that ends them, which the receiving peer acts on. | Amqp1ConnectionCases.CloseWithError_ContainerClosesAfterAttach_ReportsTheErrorWithExit8 |

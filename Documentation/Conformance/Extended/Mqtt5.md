# MQTT 5 requirement matrix: data representation, fixed header, CONNECT and CONNACK

Curl's `mqtt5://` client (`Curl.Extended.Protocol.Mqtt5.UnitLibrary`) measured against OASIS MQTT Version 5.0
(7 March 2019), chapters 1.5 (data representation), 2 (control packet format), 3.1 (CONNECT) and 3.2 (CONNACK), through the
real curl command line against the reference broker `Curl.Conformance.Extended.UnitLibrary/Mqtt5/Mqtt5ReferenceBroker`
(BL-4517). The broker checks every packet it reads against these chapters and fails the case naming the
requirement a packet breaks. IDs are the specification's own; a binding sentence without a number has a
`MQTT5-<section>-<n>` ID of its own. Server-only statements are listed as `n/a`.

The CONNACK rows (BL-4518) run curl against the broker answering with a CONNACK built by `Mqtt5/Connack/Mqtt5ConnAckBytes`. The other parts: DISCONNECT and AUTH in `Mqtt5-DisconnectAuth.md`
(BL-4519), PUBLISH, QoS and SUBSCRIBE in [Mqtt5-PublishSubscribe.md](Mqtt5-PublishSubscribe.md) (BL-4485),
and WebSocket and TLS in [Mqtt5-Transports.md](Mqtt5-Transports.md) (BL-4486).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Mqtt5:MQTT-1.5.4-1 | MUST | The character data in a UTF-8 Encoded String is well-formed UTF-8, with no surrogate code points. | Mqtt5ConnectTests.Connect_NonAsciiUserName_IsWellFormedUtf8 |
| Mqtt5:MQTT-1.5.4-2 | MUST NOT | A UTF-8 Encoded String does not include U+0000. | Mqtt5DataRepresentationTests.Publish_TopicWithNullCharacter_IsRefusedBeforeAnyPacket |
| Mqtt5:MQTT-1.5.4-3 | MUST NOT | A receiver does not skip or strip 0xEF 0xBB 0xBF (U+FEFF). | n/a: curl shows no UTF-8 string it receives; a subscriber prints only PUBLISH payloads |
| Mqtt5:MQTT-1.5.5-1 | MUST | A Variable Byte Integer uses the fewest bytes needed for its value. | Mqtt5DataRepresentationTests.Publish_LongPayloadAtQos0_FramesItWithAMinimalLengthAndNoPacketIdentifier |
| Mqtt5:MQTT-1.5.7-1 | MUST | Both strings of a UTF-8 String Pair follow the UTF-8 Encoded String rules. | n/a: curl sends no User Property, the only String Pair a client sends |
| Mqtt5:MQTT-2.1.3-1 | MUST | A fixed header flag marked Reserved carries the value table 2-2 lists. | Mqtt5DataRepresentationTests.Subscribe_WithoutData_SendsReservedFlagsAndANonZeroPacketIdentifier |
| Mqtt5:MQTT-2.2.1-2 | MUST NOT | A PUBLISH at QoS 0 carries no Packet Identifier. | Mqtt5DataRepresentationTests.Publish_LongPayloadAtQos0_FramesItWithAMinimalLengthAndNoPacketIdentifier |
| Mqtt5:MQTT-2.2.1-3 | MUST | A client gives each new SUBSCRIBE, UNSUBSCRIBE or QoS > 0 PUBLISH a non-zero unused Packet Identifier. | Mqtt5DataRepresentationTests.Subscribe_WithoutData_SendsReservedFlagsAndANonZeroPacketIdentifier |
| Mqtt5:MQTT-2.2.1-4 | MUST | A server gives each new QoS > 0 PUBLISH a non-zero unused Packet Identifier. | n/a: binds the server |
| Mqtt5:MQTT-2.2.1-5 | MUST | A PUBACK, PUBREC, PUBREL or PUBCOMP carries its PUBLISH's Packet Identifier. | n/a: curl subscribes at QoS 0 and publishes at QoS 0, so it sends none of them |
| Mqtt5:MQTT-2.2.1-6 | MUST | A SUBACK or UNSUBACK carries its request's Packet Identifier. | n/a: binds the server |
| Mqtt5:MQTT-2.2.2-1 | MUST | A packet with no properties carries a Property Length of zero. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT5-2.2.2-1 | MUST | A packet carries only the properties table 2-4 lists for it, each at most once except User Property (section 2.2.2.2 and the "Protocol Error to include ... more than once" sentences of 3.1.2.11). | n/a: curl sends no CONNECT property; the broker checks it on every CONNECT (Mqtt5ReceivedConnect) |
| Mqtt5:MQTT-3.1.0-1 | MUST | The client's first packet on a connection is CONNECT. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.0-2 | MUST | The server treats a second CONNECT as a Protocol Error. | n/a: binds the server; the broker also fails a case whose client sends CONNECT twice |
| Mqtt5:MQTT-3.1.2-1 | MUST | The Protocol Name is the UTF-8 String "MQTT". | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT5-3.1.2.2-1 | MUST | The Protocol Version of MQTT 5.0 is 5 (section 3.1.2.2). | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-2 | MUST | A server that does not support the Protocol Version answers CONNACK 0x84 and closes. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-3 | MUST | The reserved Connect Flag is 0. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-4 | MUST | With Clean Start set, client and server discard any existing Session and start a new one. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-5 | MUST | Without Clean Start, a server with a Session resumes it. | n/a: binds the server; curl always sets Clean Start |
| Mqtt5:MQTT-3.1.2-6 | MUST | Without Clean Start, a server without a Session creates one. | n/a: binds the server; curl always sets Clean Start |
| Mqtt5:MQTT-3.1.2-7 | MUST | With the Will Flag set, the server stores the Will Message. | n/a: binds the server; curl sends no Will |
| Mqtt5:MQTT-3.1.2-8 | MUST | The server publishes the Will Message when the connection closes without DISCONNECT 0x00. | n/a: binds the server; curl sends no Will |
| Mqtt5:MQTT-3.1.2-9 | MUST | With the Will Flag set, the Will Properties, Will Topic and Will Payload are in the payload. | n/a: curl never sets the Will Flag (the CONNECT case shows it 0) |
| Mqtt5:MQTT-3.1.2-10 | MUST | The server removes the Will Message once published or on DISCONNECT 0x00. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-11 | MUST | Without the Will Flag, Will QoS is 0. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-12 | MUST NOT | With the Will Flag set, Will QoS is not 3. | n/a: curl never sets the Will Flag; the broker checks Will QoS on every CONNECT |
| Mqtt5:MQTT-3.1.2-13 | MUST | Without the Will Flag, Will Retain is 0. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-14 | MUST | With Will Retain 0, the server publishes the Will Message as non-retained. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-15 | MUST | With Will Retain 1, the server publishes the Will Message as retained. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-16 | MUST NOT | Without the User Name Flag, no User Name is in the payload. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.2-17 | MUST | With the User Name Flag, a User Name is in the payload. | Mqtt5ConnectTests.Connect_UserAndPassword_SetsBothFlagsAndSendsBothFieldsLast |
| Mqtt5:MQTT-3.1.2-18 | MUST NOT | Without the Password Flag, no Password is in the payload. | Mqtt5ConnectTests.Connect_UserWithEmptyPassword_SendsUserNameOnly |
| Mqtt5:MQTT-3.1.2-19 | MUST | With the Password Flag, a Password is in the payload. | Mqtt5ConnectTests.Connect_UserAndPassword_SetsBothFlagsAndSendsBothFieldsLast |
| Mqtt5:MQTT-3.1.2-20 | MUST | With a non-zero Keep Alive and nothing else to send, the client sends PINGREQ within it. | Mqtt5ConnectTests.Connect_KeepAlive_IsZeroSoNoPingIsOwed |
| Mqtt5:MQTT-3.1.2-21 | MUST | The client uses the CONNACK's Server Keep Alive in place of its own. | n/a: a CONNACK property; measured as Mqtt5:MQTT-3.2.2-21 |
| Mqtt5:MQTT-3.1.2-22 | MUST | A server that hears nothing for one and a half Keep Alives closes the connection. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-23 | MUST | Client and server keep the Session State after the connection closes while the Session Expiry Interval runs. | n/a: curl sends no Session Expiry Interval, so the session ends with the connection |
| Mqtt5:MQTT-3.1.2-24 | MUST NOT | The server sends no packet over the client's Maximum Packet Size. | n/a: binds the server; curl sends no Maximum Packet Size |
| Mqtt5:MQTT-3.1.2-25 | MUST | The server discards a packet too large to send. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-26 | MUST NOT | The server sends no Topic Alias over the client's Topic Alias Maximum. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-27 | MUST NOT | Without a Topic Alias Maximum, the server sends no Topic Alias. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-28 | MUST NOT | With Request Response Information 0, the server returns no Response Information. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-29 | MUST NOT | With Request Problem Information 0, the server sends a Reason String or User Property only on PUBLISH, CONNACK or DISCONNECT. | n/a: binds the server |
| Mqtt5:MQTT-3.1.2-30 | MUST NOT | A client that set an Authentication Method sends only AUTH or DISCONNECT until CONNACK. | n/a: curl sets no Authentication Method |
| Mqtt5:MQTT-3.1.3-1 | MUST | The payload fields appear in the order Client Identifier, Will Properties, Will Topic, Will Payload, User Name, Password. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.3-2 | MUST | Client and server identify the Session State by the ClientID. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.3-3 | MUST | The ClientID is present and the first payload field. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.3-4 | MUST | The ClientID is a UTF-8 Encoded String. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.3-5 | MUST | The server accepts ClientIDs of 1 to 23 bytes of [0-9a-zA-Z]; curl's is one of those. | Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst |
| Mqtt5:MQTT-3.1.3-6 | MUST | A server allowing a zero-length ClientID treats it as a special case and assigns one. | n/a: binds the server; curl sends a non-empty ClientID |
| Mqtt5:MQTT-3.1.3-7 | MUST | A server assigning a ClientID gives a unique one. | n/a: binds the server |
| Mqtt5:MQTT-3.1.3-8 | MUST | A server rejecting the ClientID closes the connection. | n/a: binds the server |
| Mqtt5:MQTT-3.1.3-9 | MUST NOT | A server does not send the Will Message when the session reconnects within the Will Delay Interval. | n/a: binds the server |
| Mqtt5:MQTT-3.1.3-10 | MUST | The server keeps the order of User Properties when forwarding. | n/a: binds the server |
| Mqtt5:MQTT-3.1.3-11 | MUST | The Will Topic is a UTF-8 Encoded String. | n/a: curl sends no Will; the broker reads a Will Topic as a UTF-8 Encoded String |
| Mqtt5:MQTT-3.1.3-12 | MUST | With the User Name Flag, the User Name is a UTF-8 Encoded String. | Mqtt5ConnectTests.Connect_UserAndPassword_SetsBothFlagsAndSendsBothFieldsLast |
| Mqtt5:MQTT-3.1.4-1 | MUST | The server validates CONNECT against section 3.1 and closes on a malformed one. | n/a: binds the server |
| Mqtt5:MQTT-3.1.4-2 | MUST | A server whose further checks fail closes the connection. | n/a: binds the server |
| Mqtt5:MQTT-3.1.4-3 | MUST | A server whose ClientID is already connected sends that connection DISCONNECT 0x8E. | n/a: binds the server |
| Mqtt5:MQTT-3.1.4-4 | MUST | The server processes Clean Start. | n/a: binds the server |
| Mqtt5:MQTT-3.1.4-5 | MUST | The server acknowledges CONNECT with CONNACK 0x00. | n/a: binds the server |
| Mqtt5:MQTT-3.1.4-6 | MUST NOT | A server that rejected CONNECT processes nothing after it but AUTH. | n/a: binds the server |
| Mqtt5:MQTT-3.2.0-1 | MUST | The server's first packet is CONNACK, or AUTH before it. | n/a: binds the server |
| Mqtt5:MQTT-3.2.0-2 | MUST NOT | The server sends at most one CONNACK on a connection. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-1 | MUST | The reserved Connect Acknowledge Flags (bits 7-1) are 0; the client treats others as a Malformed Packet. | ConnackCases.Connack_ReservedAcknowledgeFlagSet_IsAMalformedPacketWithExit8 |
| Mqtt5:MQTT-3.2.2-2 | MUST | A server accepting a Clean Start sets Session Present 0. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-3 | MUST | A server resuming a session without Clean Start sets Session Present 1. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-4 | MUST | A client without Session State that receives Session Present 1 closes the connection. | ConnackCases.Connack_SessionPresentAfterCleanStart_ClosesTheConnectionWithExit8 |
| Mqtt5:MQTT-3.2.2-5 | MUST | A client with Session State that receives Session Present 0 discards it. | n/a: curl keeps no Session State between transfers |
| Mqtt5:MQTT-3.2.2-6 | MUST | A CONNACK with a non-zero Reason Code has Session Present 0. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-7 | MUST | A server sending a Reason Code of 0x80 or above closes the connection. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-8 | MUST | The server sends a Connect Reason Code table 3-1 lists. | n/a: binds the server |
| Mqtt5:MQTT5-3.2.2.2-1 | MUST | A client receiving a Connect Reason Code of 0x80 or above ends the connection and sends nothing after CONNECT (sections 3.2.2.2 and 4.13). | ConnackCases.Connack_FailureReasonCode_EndsTheTransferAfterConnectWithExit8 |
| Mqtt5:MQTT-3.2.2-9 | MUST | A server not supporting QoS 1 or 2 sends Maximum QoS. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-10 | MUST | A server still accepts a SUBSCRIBE above its Maximum QoS. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-11 | MUST NOT | The client sends no PUBLISH above the Maximum QoS. | ConnackCases.Connack_ReceiveMaximumOneMaximumQos0NoRetainNoTopicAlias_PublishesAtQos0UnretainedWithNoAlias |
| Mqtt5:MQTT-3.2.2-12 | MUST | A server rejects a CONNECT whose Will QoS exceeds its capability. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-13 | MUST | A server without retain support rejects a CONNECT with Will Retain 1. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-14 | MUST NOT | With Retain Available 0, the client sends no PUBLISH with RETAIN 1. | ConnackCases.Connack_ReceiveMaximumOneMaximumQos0NoRetainNoTopicAlias_PublishesAtQos0UnretainedWithNoAlias |
| Mqtt5:MQTT-3.2.2-15 | MUST NOT | The client sends no packet over the server's Maximum Packet Size. | ConnackCases.Connack_MaximumPacketSizeBelowThePublish_SendsNoPublishAndExits55 |
| Mqtt5:MQTT-3.2.2-16 | MUST | A server assigning a ClientID returns it as Assigned Client Identifier. | n/a: binds the server |
| Mqtt5:MQTT5-3.2.2.3.3-1 | MUST | The client sends no more unacknowledged QoS 1 and 2 PUBLISH packets than the Receive Maximum (sections 3.2.2.3.3 and 4.9). | ConnackCases.Connack_ReceiveMaximumOneMaximumQos0NoRetainNoTopicAlias_PublishesAtQos0UnretainedWithNoAlias |
| Mqtt5:MQTT5-3.2.2.3.7-1 | MUST | The client accepts a CONNACK carrying an Assigned Client Identifier (section 3.2.2.3.7). | ConnackCases.Connack_AssignedClientIdentifier_IsAcceptedAndThePublishSent |
| Mqtt5:MQTT-3.2.2-17 | MUST NOT | The client sends no Topic Alias above the Topic Alias Maximum. | ConnackCases.Connack_TopicAliasMaximum_PublishesWithNoTopicAlias |
| Mqtt5:MQTT-3.2.2-18 | MUST NOT | Without a Topic Alias Maximum, the client sends no Topic Alias. | ConnackCases.Connack_ReceiveMaximumOneMaximumQos0NoRetainNoTopicAlias_PublishesAtQos0UnretainedWithNoAlias |
| Mqtt5:MQTT-3.2.2-19 | MUST NOT | The server sends no Reason String that takes CONNACK over the client's Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-20 | MUST NOT | The server sends no User Property that takes CONNACK over the client's Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.2.2-21 | MUST | The client uses the CONNACK's Server Keep Alive in place of its own Keep Alive. | ConnackCases.Connack_ServerKeepAlive_IsUsedSoASubscriberPings |
| Mqtt5:MQTT-3.2.2-22 | MUST | Without a Server Keep Alive, the server uses the client's Keep Alive. | n/a: binds the server |
| Mqtt5:MQTT5-3.2.2.3-1 | MUST | A CONNACK property other than User Property appears at most once; a repeat is a Protocol Error the client refuses (section 3.2.2.3). | ConnackCases.Connack_PropertyTwice_IsRefusedWithExit8 |

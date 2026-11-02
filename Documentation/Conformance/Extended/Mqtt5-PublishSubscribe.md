# MQTT 5 requirement matrix: PUBLISH, QoS flows, SUBSCRIBE, keep alive and topic rules

Curl's `mqtt5://` client (`Curl.Extended.Protocol.Mqtt5.UnitLibrary`) measured against OASIS MQTT Version 5.0
(7 March 2019) sections 3.3 to 3.13 (PUBLISH, PUBACK, PUBREC, PUBREL, PUBCOMP, SUBSCRIBE, SUBACK, UNSUBSCRIBE,
UNSUBACK, PINGREQ, PINGRESP), 3.1.2.10 (keep alive), 4.3 to 4.9 (QoS flows, redelivery, ordering, topics,
subscriptions, flow control) through the real curl command line (BL-4485). The cases in
`Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/` run curl against the reference broker
(`Mqtt5/Mqtt5ReferenceBroker`, BL-4517) answering with the packets `Mqtt5/PublishSubscribe/Mqtt5PublishSubscribeBytes`
builds. IDs are the specification's own; a binding sentence without a number has a `MQTT5-<section>-<n>` ID of its own.

curl publishes one message at QoS 0 with `-d`, or subscribes to one filter at QoS 0 and writes each message until the
server's DISCONNECT. It never sends a QoS 1 or 2 PUBLISH, an UNSUBSCRIBE, a Topic Alias Maximum, a Receive Maximum or a
Maximum Packet Size, and always sets Clean Start, so the statements binding a QoS > 0 sender, an unsubscriber or a
resumed session are `n/a`. The keep-alive cases run on real time with a one-second Server Keep Alive and `-m` as a
guard; their cost is a few seconds. The client's end of a missing PINGRESP and of a Topic Alias it never allowed are
decided in ADR-1197.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Mqtt5:MQTT-3.3.1-1 | MUST | DUP is 0 on the first attempt to send a PUBLISH. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.1-2 | MUST | DUP is 0 on every QoS 0 message. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.1-3 | MUST | The DUP of an outgoing PUBLISH is set independently of the incoming one. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-4 | MUST NOT | A PUBLISH does not set both QoS bits; a receiver treats it as malformed. | ReceivedPublishCases.Received_BothQosBitsSet_IsAMalformedPacketWithExit8 |
| Mqtt5:MQTT-3.3.1-5 | MUST | A retained message replaces the one stored for its topic. | n/a: binds the server; curl never sets RETAIN |
| Mqtt5:MQTT-3.3.1-6 | MUST | A retained zero-byte message removes the stored one. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-7 | MUST NOT | A zero-byte retained message is not stored. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-8 | MUST NOT | A message with RETAIN 0 is not stored as retained. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-9 | MUST | Retain Handling 0 sends the retained messages on subscribe. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-10 | MUST | Retain Handling 1 sends them only for a new subscription. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-11 | MUST NOT | Retain Handling 2 sends none. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-12 | MUST | Retain As Published 0 forwards RETAIN as 0. | n/a: binds the server |
| Mqtt5:MQTT-3.3.1-13 | MUST | Retain As Published 1 forwards RETAIN as published. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-1 | MUST | The Topic Name is the first field of the variable header, a UTF-8 Encoded String. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.2-2 | MUST NOT | A Topic Name holds no wildcard character. | PublishCases.Publish_WildcardInTheTopicName_IsRefusedBeforeAnyPacketWithExit3 |
| Mqtt5:MQTT5-3.3.2.1-1 | MUST | A receiver treats a PUBLISH whose Topic Name holds a wildcard as malformed (sections 3.3.2.1 and 4.7). | ReceivedPublishCases.Received_WildcardInTheTopicName_IsAMalformedPacketWithExit8 |
| Mqtt5:MQTT-3.3.2-3 | MUST | The Topic Name sent to a subscriber matches its filter. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-4 | MUST | The server forwards the Payload Format Indicator unaltered. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-5 | MUST | The server deletes an expired message. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-6 | MUST | The server forwards the Message Expiry Interval less the time it waited. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-7 | MUST NOT | A receiver carries no Topic Alias mapping from one connection to the next. | n/a: curl makes one connection per transfer and keeps no alias mapping |
| Mqtt5:MQTT-3.3.2-8 | MUST NOT | A sender sends no Topic Alias of 0. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.2-9 | MUST NOT | A client sends no Topic Alias above the server's Topic Alias Maximum. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.2-10 | MUST | A client accepts every Topic Alias from 1 to the Topic Alias Maximum it sent. | n/a: curl sends no Topic Alias Maximum, so it accepts none (Mqtt5:MQTT5-3.3.2.3.4-1) |
| Mqtt5:MQTT5-3.3.2.3.4-1 | MUST | A receiver treats a Topic Alias of 0 or above its Topic Alias Maximum as a Protocol Error and sends DISCONNECT 0x94 Topic Alias invalid. | ReceivedPublishCases.Received_TopicAliasSetAndReused_IsRefusedWithDisconnect0x94 |
| Mqtt5:MQTT-3.3.2-11 | MUST NOT | The server sends no Topic Alias above the client's Topic Alias Maximum. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-12 | MUST | The server accepts every Topic Alias up to its own maximum. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-13 | MUST | The Response Topic is a UTF-8 Encoded String. | n/a: curl sends no Response Topic |
| Mqtt5:MQTT-3.3.2-14 | MUST NOT | The Response Topic holds no wildcard. | n/a: curl sends no Response Topic |
| Mqtt5:MQTT-3.3.2-15 | MUST | The server forwards the Response Topic unaltered. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-16 | MUST | The server forwards the Correlation Data unaltered. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-17 | MUST | The server forwards every User Property unaltered. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-18 | MUST | The server keeps the User Properties in order. | n/a: binds the server |
| Mqtt5:MQTT-3.3.2-19 | MUST | The Content Type is a UTF-8 Encoded String. | n/a: curl sends no Content Type |
| Mqtt5:MQTT-3.3.2-20 | MUST | The server forwards the Content Type unaltered. | n/a: binds the server |
| Mqtt5:MQTT5-3.3.2.3-1 | MUST | A client accepts every property table 2-4 lists for PUBLISH (Payload Format Indicator, Message Expiry Interval, Content Type, Response Topic, Correlation Data, User Property, Subscription Identifier). | ReceivedPublishCases.Received_PublishWithEveryProperty_WritesThePayload |
| Mqtt5:MQTT-3.3.4-1 | MUST | The receiver of a PUBLISH answers it as its QoS requires (table 3-4). | ReceivedPublishCases.Received_Qos0Messages_AreWrittenInOrderAndNotAcknowledged |
| Mqtt5:MQTT-3.3.4-2 | MUST | The server delivers at the maximum QoS of the matching subscriptions. | n/a: binds the server |
| Mqtt5:MQTT-3.3.4-3 | MUST | The server sends the Subscription Identifiers of the matching subscriptions. | n/a: binds the server |
| Mqtt5:MQTT-3.3.4-4 | MUST | Each copy for overlapping subscriptions carries its own identifier. | n/a: binds the server |
| Mqtt5:MQTT-3.3.4-5 | MUST | One copy for overlapping subscriptions carries all their identifiers. | n/a: binds the server |
| Mqtt5:MQTT-3.3.4-6 | MUST NOT | A client's PUBLISH carries no Subscription Identifier. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.4-7 | MUST NOT | A client sends no more unacknowledged QoS > 0 PUBLISH than the server's Receive Maximum. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-3.3.4-8 | MUST NOT | A client delays no packet but PUBLISH because of Receive Maximum. | n/a: curl sends no QoS > 0 PUBLISH, so its quota never runs out |
| Mqtt5:MQTT-3.3.4-9 | MUST NOT | The server sends no more unacknowledged QoS > 0 PUBLISH than the client's Receive Maximum. | n/a: binds the server |
| Mqtt5:MQTT-3.3.4-10 | MUST NOT | The server delays no packet but PUBLISH because of Receive Maximum. | n/a: binds the server |
| Mqtt5:MQTT5-4.3.1-1 | MUST | A QoS 0 PUBLISH is not acknowledged, so its sender waits for no PUBACK (section 4.3.1, table 3-4). | PublishCases.Publish_Qos0AnsweredWithAPuback_DoesNotWaitForItAndExits0 |
| Mqtt5:MQTT-3.4.2-1 | MUST | A PUBACK carries one of the PUBACK Reason Codes. | n/a: curl sends PUBACK only for QoS 1 (Mqtt5:MQTT-4.3.2-4) |
| Mqtt5:MQTT-3.4.2-2 | MUST NOT | A Reason String is not added past the Maximum Packet Size. | n/a: curl sends no Reason String |
| Mqtt5:MQTT-3.4.2-3 | MUST NOT | A User Property is not added past the Maximum Packet Size. | n/a: curl sends no User Property |
| Mqtt5:MQTT-3.5.2-1 | MUST | A PUBREC carries one of the PUBREC Reason Codes. | n/a: measured as Mqtt5:MQTT-4.3.3-8 |
| Mqtt5:MQTT-3.5.2-2 | MUST NOT | A Reason String is not added past the Maximum Packet Size. | n/a: curl sends no Reason String |
| Mqtt5:MQTT-3.5.2-3 | MUST NOT | A User Property is not added past the Maximum Packet Size. | n/a: curl sends no User Property |
| Mqtt5:MQTT-3.6.1-1 | MUST | PUBREL's fixed header flags are 0010. | n/a: curl sends no QoS 2 PUBLISH, so never a PUBREL |
| Mqtt5:MQTT-3.6.2-1 | MUST | A PUBREL carries one of the PUBREL Reason Codes. | n/a: curl sends no PUBREL |
| Mqtt5:MQTT-3.6.2-2 | MUST NOT | A Reason String is not added past the Maximum Packet Size. | n/a: curl sends no PUBREL |
| Mqtt5:MQTT-3.6.2-3 | MUST NOT | A User Property is not added past the Maximum Packet Size. | n/a: curl sends no PUBREL |
| Mqtt5:MQTT-3.7.2-1 | MUST | A PUBCOMP carries one of the PUBCOMP Reason Codes. | n/a: measured as Mqtt5:MQTT-4.3.3-11 |
| Mqtt5:MQTT-3.7.2-2 | MUST NOT | A Reason String is not added past the Maximum Packet Size. | n/a: curl sends no Reason String |
| Mqtt5:MQTT-3.7.2-3 | MUST NOT | A User Property is not added past the Maximum Packet Size. | n/a: curl sends no User Property |
| Mqtt5:MQTT-3.8.1-1 | MUST | SUBSCRIBE's fixed header flags are 0010. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT-3.8.3-1 | MUST | A Topic Filter is a UTF-8 Encoded String. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT-3.8.3-2 | MUST | The payload holds at least one Topic Filter and Subscription Options pair. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT-3.8.3-3 | MUST NOT | With No Local set, the server forwards nothing back to the publisher. | n/a: binds the server |
| Mqtt5:MQTT-3.8.3-4 | MUST NOT | No Local is not set on a Shared Subscription. | SubscribeCases.Subscribe_SharedSubscription_IsSentWithNoLocalClear |
| Mqtt5:MQTT-3.8.3-5 | MUST | The reserved bits of the Subscription Options are 0. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT-3.8.4-1 | MUST | The server answers SUBSCRIBE with SUBACK. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-2 | MUST | The SUBACK carries the SUBSCRIBE's Packet Identifier. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-3 | MUST | A subscription to an existing filter replaces it. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-4 | MUST | Retain Handling 0 resends the retained messages on a replaced subscription. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-5 | MUST | Several filters are handled as a sequence of SUBSCRIBEs. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-6 | MUST | The SUBACK carries a Reason Code per filter. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-7 | MUST | The granted QoS is the maximum the server grants. | n/a: binds the server |
| Mqtt5:MQTT-3.8.4-8 | MUST | Messages are delivered at the lower of the published and granted QoS. | n/a: binds the server |
| Mqtt5:MQTT5-3.9.3-1 | MUST | A client treats a SUBACK Reason Code of 0x80 or above as the subscription refused (section 3.9.3). | SubscribeCases.Subscribe_SubAckRefusesTheFilter_EndsWithExit8NamingTheReasonCode |
| Mqtt5:MQTT-3.9.2-1 | MUST NOT | The server adds no Reason String past the Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.9.2-2 | MUST NOT | The server adds no User Property past the Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.9.3-1 | MUST | The SUBACK's Reason Codes follow the SUBSCRIBE's filters in order. | n/a: binds the server |
| Mqtt5:MQTT-3.9.3-2 | MUST | The SUBACK carries one of the SUBACK Reason Codes. | n/a: binds the server |
| Mqtt5:MQTT-3.10.1-1 | MUST | UNSUBSCRIBE's fixed header flags are 0010. | n/a: curl sends no UNSUBSCRIBE |
| Mqtt5:MQTT-3.10.3-1 | MUST | An UNSUBSCRIBE's Topic Filters are UTF-8 Encoded Strings. | n/a: curl sends no UNSUBSCRIBE |
| Mqtt5:MQTT-3.10.3-2 | MUST | An UNSUBSCRIBE holds at least one Topic Filter. | n/a: curl sends no UNSUBSCRIBE |
| Mqtt5:MQTT-3.10.4-1 | MUST | The server matches the filters character by character. | n/a: binds the server |
| Mqtt5:MQTT-3.10.4-2 | MUST | The server stops sending to an unsubscribed filter. | n/a: binds the server |
| Mqtt5:MQTT-3.10.4-3 | MUST | The server completes the messages it started. | n/a: binds the server |
| Mqtt5:MQTT-3.10.4-4 | MUST | The server answers UNSUBSCRIBE with UNSUBACK. | n/a: binds the server |
| Mqtt5:MQTT-3.10.4-5 | MUST | The UNSUBACK carries the UNSUBSCRIBE's Packet Identifier. | n/a: binds the server |
| Mqtt5:MQTT-3.10.4-6 | MUST | Several filters are handled as a sequence of UNSUBSCRIBEs. | n/a: binds the server |
| Mqtt5:MQTT-3.11.2-1 | MUST NOT | The server adds no Reason String past the Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.11.2-2 | MUST NOT | The server adds no User Property past the Maximum Packet Size. | n/a: binds the server |
| Mqtt5:MQTT-3.11.3-1 | MUST | The UNSUBACK's Reason Codes follow the filters in order. | n/a: binds the server |
| Mqtt5:MQTT-3.11.3-2 | MUST | The UNSUBACK carries one of the UNSUBACK Reason Codes. | n/a: binds the server |
| Mqtt5:MQTT-3.12.4-1 | MUST | The server answers PINGREQ with PINGRESP. | n/a: binds the server |
| Mqtt5:MQTT5-3.12-1 | MUST | While it waits, the client sends PINGREQ again each keep-alive period and accepts each PINGRESP (sections 3.1.2.10 and 3.12). | KeepAliveCases.Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp |
| Mqtt5:MQTT5-3.1.2.10-1 | SHOULD | A client that receives no PINGRESP within a reasonable time after PINGREQ closes the connection (section 3.1.2.10). | KeepAliveCases.Subscribe_NoPingrespWithinTheInterval_ClosesTheConnectionWithDisconnect0x8D |
| Mqtt5:MQTT5-3.1.2.11.4-1 | MUST | Without a Maximum Packet Size the receiver imposes no limit beyond the protocol's, so a client that sends none accepts any PUBLISH (section 3.1.2.11.4). | ReceivedPublishCases.Received_MessageLargerThan64KiB_IsWrittenWholeSinceCurlAnnouncesNoMaximumPacketSize |
| Mqtt5:MQTT-4.3.1-1 | MUST | A QoS 0 sender sends PUBLISH with QoS 0 and DUP 0. | PublishCases.Publish_SendsQos0WithNoDupRetainOrPropertiesThenDisconnects |
| Mqtt5:MQTT-4.3.2-1 | MUST | A QoS 1 sender gives each new message an unused Packet Identifier. | n/a: curl sends no QoS 1 PUBLISH |
| Mqtt5:MQTT-4.3.2-2 | MUST | A QoS 1 sender sends PUBLISH with QoS 1 and DUP 0. | n/a: curl sends no QoS 1 PUBLISH |
| Mqtt5:MQTT-4.3.2-3 | MUST | A QoS 1 sender treats the PUBLISH as unacknowledged until its PUBACK. | n/a: curl sends no QoS 1 PUBLISH |
| Mqtt5:MQTT-4.3.2-4 | MUST | A QoS 1 receiver answers with a PUBACK carrying the PUBLISH's Packet Identifier. | ReceivedPublishCases.Received_Qos1Messages_AreEachAcknowledgedWithAPubackInOrder |
| Mqtt5:MQTT-4.3.2-5 | MUST | After its PUBACK, a receiver treats a PUBLISH with the same identifier as new. | n/a: depends on Mqtt5:MQTT-4.3.2-4 (BL-4580) |
| Mqtt5:MQTT-4.3.3-1 | MUST | A QoS 2 sender gives each new message an unused Packet Identifier. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-2 | MUST | A QoS 2 sender sends PUBLISH with QoS 2 and DUP 0. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-3 | MUST | A QoS 2 sender treats the PUBLISH as unacknowledged until its PUBREC. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-4 | MUST | A QoS 2 sender answers PUBREC with PUBREL. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-5 | MUST | A QoS 2 sender treats the PUBREL as unacknowledged until its PUBCOMP. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-6 | MUST NOT | A QoS 2 sender resends no PUBLISH once it sent PUBREL. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-7 | MUST NOT | A QoS 2 sender applies no Message Expiry once it sent PUBREL. | n/a: curl sends no QoS 2 PUBLISH |
| Mqtt5:MQTT-4.3.3-8 | MUST | A QoS 2 receiver answers with a PUBREC carrying the PUBLISH's Packet Identifier. | ReceivedPublishCases.Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp |
| Mqtt5:MQTT-4.3.3-9 | MUST | After a failure PUBREC, a receiver treats a PUBLISH with the same identifier as new. | n/a: depends on Mqtt5:MQTT-4.3.3-8 (BL-4580) |
| Mqtt5:MQTT-4.3.3-10 | MUST | Until PUBREL, a receiver answers a repeated PUBLISH with PUBREC and delivers it once. | n/a: depends on Mqtt5:MQTT-4.3.3-8 (BL-4580) |
| Mqtt5:MQTT-4.3.3-11 | MUST | A QoS 2 receiver answers PUBREL with a PUBCOMP carrying its Packet Identifier. | ReceivedPublishCases.Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp |
| Mqtt5:MQTT-4.3.3-12 | MUST | After its PUBCOMP, a receiver treats a PUBLISH with the same identifier as new. | n/a: depends on Mqtt5:MQTT-4.3.3-11 (BL-4580) |
| Mqtt5:MQTT-4.3.3-13 | MUST | A receiver completes the QoS 2 flow even when the message expired. | n/a: depends on Mqtt5:MQTT-4.3.3-8 (BL-4580) |
| Mqtt5:MQTT-4.4.0-1 | MUST | On a resumed session the client resends unacknowledged PUBLISH with DUP 1 and PUBREL. | n/a: curl always sets Clean Start and refuses Session Present (Mqtt5:MQTT-3.2.2-4) |
| Mqtt5:MQTT-4.4.0-2 | MUST NOT | A PUBACK or PUBREC failure completes the message; it is not resent. | n/a: curl sends no QoS > 0 PUBLISH |
| Mqtt5:MQTT-4.5.0-1 | MUST | The server adds a matching message to each subscriber's session. | n/a: binds the server |
| Mqtt5:MQTT-4.5.0-2 | MUST | The client acknowledges each message it receives as its QoS requires. | ReceivedPublishCases.Received_Qos0Messages_AreWrittenInOrderAndNotAcknowledged |
| Mqtt5:MQTT-4.6.0-1 | MUST | Resent PUBLISH keep their original order. | n/a: curl resends nothing |
| Mqtt5:MQTT-4.6.0-2 | MUST | PUBACKs go in the order the QoS 1 PUBLISH arrived. | ReceivedPublishCases.Received_Qos1Messages_AreEachAcknowledgedWithAPubackInOrder |
| Mqtt5:MQTT-4.6.0-3 | MUST | PUBRECs go in the order the QoS 2 PUBLISH arrived. | ReceivedPublishCases.Received_Qos2Message_IsAcknowledgedWithPubrecThenPubcomp |
| Mqtt5:MQTT-4.6.0-4 | MUST | PUBRELs go in the order the PUBRECs arrived. | n/a: curl sends no PUBREL |
| Mqtt5:MQTT-4.6.0-5 | MUST | The server treats each topic as an Ordered Topic by default. | n/a: binds the server |
| Mqtt5:MQTT-4.6.0-6 | MUST | The server forwards an Ordered Topic's messages in order. | n/a: binds the server |
| Mqtt5:MQTT-4.7.0-1 | MUST NOT | Wildcards are used in Topic Filters, never in Topic Names. | PublishCases.Publish_WildcardInTheTopicName_IsRefusedBeforeAnyPacketWithExit3 |
| Mqtt5:MQTT-4.7.1-1 | MUST | `#` is the last character of a filter, alone or after `/`. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT-4.7.1-2 | MUST | `+` occupies a whole level of a filter. | SubscribeCases.Subscribe_SendsOneFilterAtQos0WithReservedBitsClear |
| Mqtt5:MQTT5-4.7.1-1 | MUST | A filter with a wildcard not alone in its level is not sent (sections 4.7.1.2 and 4.7.1.3). | SubscribeCases.Subscribe_WildcardNotAloneInItsLevel_IsRefusedBeforeAnyPacketWithExit3 |
| Mqtt5:MQTT-4.7.2-1 | MUST NOT | The server matches no `$` topic to a filter starting with a wildcard. | n/a: binds the server |
| Mqtt5:MQTT-4.7.3-1 | MUST | Topic Names and Topic Filters are at least one character long. | PublishCases.Publish_EmptyTopic_IsRefusedBeforeAnyPacketWithExit3 |
| Mqtt5:MQTT-4.7.3-2 | MUST NOT | Topic Names and Topic Filters hold no U+0000. | n/a: measured as Mqtt5:MQTT-1.5.4-2 (Mqtt5.md) |
| Mqtt5:MQTT-4.7.3-3 | MUST NOT | Topic Names and Topic Filters encode to no more than 65535 bytes. | n/a: a UTF-8 Encoded String's two-byte length; measured as Mqtt5:MQTT-1.5.4-1 (Mqtt5.md) |
| Mqtt5:MQTT-4.7.3-4 | MUST NOT | The server normalises no topic when matching. | n/a: binds the server |
| Mqtt5:MQTT-4.8.2-1 | MUST | A Shared Subscription filter starts `$share/` and has a ShareName of at least one character. | SubscribeCases.Subscribe_SharedSubscription_IsSentWithNoLocalClear |
| Mqtt5:MQTT-4.8.2-2 | MUST | The ShareName holds no `/`, `+` or `#` and is followed by `/` and a filter. | SubscribeCases.Subscribe_SharedSubscription_IsSentWithNoLocalClear |
| Mqtt5:MQTT5-4.8.2-1 | MUST | A Shared Subscription filter without a valid ShareName is not sent (section 4.8.2). | SubscribeCases.Subscribe_SharedSubscriptionWithoutAValidShareName_IsRefusedBeforeAnyPacketWithExit3 |
| Mqtt5:MQTT-4.8.2-3 | MUST | The server sends each shared message to one session only. | n/a: binds the server |
| Mqtt5:MQTT-4.8.2-4 | MUST | The server completes delivery to a reconnecting session. | n/a: binds the server |
| Mqtt5:MQTT-4.8.2-5 | MUST | The server gives up a message whose session ended. | n/a: binds the server |
| Mqtt5:MQTT-4.8.2-6 | MUST NOT | A PUBACK or PUBREC failure is not resent to another subscriber. | n/a: binds the server |
| Mqtt5:MQTT-4.9.0-1 | MUST | The send quota starts at Receive Maximum. | n/a: curl sends no QoS > 0 PUBLISH |
| Mqtt5:MQTT-4.9.0-2 | MUST NOT | No QoS > 0 PUBLISH is sent once the send quota is 0. | n/a: curl sends no QoS > 0 PUBLISH |
| Mqtt5:MQTT-4.9.0-3 | MUST | Other packets are sent even when the send quota is 0. | n/a: curl sends no QoS > 0 PUBLISH |

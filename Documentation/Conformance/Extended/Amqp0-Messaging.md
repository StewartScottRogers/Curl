# AMQP 0-9-1: channels, queue.declare, basic.publish, basic.get and content

Curl's `amqp://` client (`Curl.Extended.Protocol.Amqp0.UnitLibrary`) against the AMQP 0-9-1 specification
(2008-11-13) with RabbitMQ's errata, part 2 of 2 (BL-4465): the channel class, `queue.declare`, `basic.publish`,
`basic.get`, `get-ok` and `get-empty`, `basic.return`, the content header (4.2.6.1) and body frames (4.2.6.2), and
channel exceptions. The protocol header, framing and the connection class are in [Amqp0.md](Amqp0.md) (BL-4464).
The cases are in `Curl.Conformance.Extended.UnitTests/Amqp0/Messaging/` and run against `Amqp0ReferenceBroker`
with the answers of `Amqp0MessagingMethods` (`Curl.Conformance.Extended.UnitLibrary/Amqp0/Messaging/`), added
through `Amqp0MethodTable.With`. `1.x.y.z` locates a method's rules in the XML specification's documentation
(1.1 reply codes, 1.5 channel, 1.7 queue, 1.8 basic); `4.2.6` is the content framing of the protocol document.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Amqp0:AMQP091-1.1-1 | SHOULD | A channel exception's reply code is reported: 403 (access-refused) as a refused login, any other (404 not-found, 405 resource-locked, 406 precondition-failed) as a failed transfer naming the code and text. | Amqp0MessagingTests.Declare_ChannelClosedByTheBroker_IsAnsweredWithCloseOkAndMapsTheReplyCode |
| Amqp0:AMQP091-1.5.2.1-1 | MUST | The client opens a channel other than 0 with channel.open (reserved-1 empty) and waits for open-ok before any other method on it. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-1.5.2.3-1 | MUST | A client that receives channel.flow answers with channel.flow-ok carrying the same active bit. | Amqp0MessagingTests.Flow_FromTheBroker_IsAnsweredWithFlowOk |
| Amqp0:AMQP091-1.5.2.4-1 | MUST | A peer that sends channel.flow waits for flow-ok. | n/a: the client never sends channel.flow; it only receives it |
| Amqp0:AMQP091-1.5.2.5-1 | MUST | The client ends its work by closing the channel with channel.close (reply-success 200) and waits for close-ok before closing the connection. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-1.5.2.6-1 | MUST | A peer that receives channel.close answers with channel.close-ok on that channel. | Amqp0MessagingTests.Declare_ChannelClosedByTheBroker_IsAnsweredWithCloseOkAndMapsTheReplyCode |
| Amqp0:AMQP091-1.5.2.6-2 | MUST NOT | After channel.close the peer sends no more methods or content on that channel. | Amqp0MessagingTests.Declare_ChannelClosedByTheBroker_IsAnsweredWithCloseOkAndMapsTheReplyCode |
| Amqp0:AMQP091-1.7.2.1-1 | MUST | queue.declare carries reserved-1 0 and the queue name of the URL. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-1.8.3.7-1 | MUST | basic.publish carries reserved-1 0, the default exchange "" and the queue as routing key; mandatory and immediate are clear, since RabbitMQ refuses immediate. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-1.8.3.9-1 | MUST | A client accepts basic.return and the content that follows it as a content-bearing method, not as a protocol error, and still closes the channel and connection cleanly. | Amqp0MessagingTests.Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly |
| Amqp0:AMQP091-1.8.3.10-1 | MUST | basic.get carries reserved-1 0, the queue name and the no-ack bit. | Amqp0MessagingTests.Get_GetOkWithAMessageInSeveralFrames_WritesTheBodyAndSendsNoAck |
| Amqp0:AMQP091-1.8.3.11-1 | MUST | After get-ok the client reads the message's content and, having asked for no-ack, sends no basic.ack. | Amqp0MessagingTests.Get_GetOkWithAMessageInSeveralFrames_WritesTheBodyAndSendsNoAck |
| Amqp0:AMQP091-1.8.3.12-1 | MUST | get-empty means the queue had no message: the client expects no content and ends without error. | Amqp0MessagingTests.Get_GetEmpty_EndsWithNoOutput |
| Amqp0:AMQP091-1.8.3.13-1 | MUST | A delivery tag acknowledged with basic.ack is one the channel delivered. | n/a: the client gets with no-ack and never sends basic.ack |
| Amqp0:AMQP091-1.8.3.3-1 | MUST | basic.consume and basic.deliver run the consumer flow. | n/a: the client takes one message with basic.get; it never consumes |
| Amqp0:AMQP091-4.2.6-1 | MUST | A content-bearing method is followed at once by its content header frame and then its body frames, all on the method's channel. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-4.2.6-2 | MUST | Any frame on another channel or of another type before the content is whole is an unexpected-frame connection exception: connection.close with 505. | Amqp0MessagingTests.Get_BodyFrameOnAnotherChannel_IsAnUnexpectedFrameClosedWith505 |
| Amqp0:AMQP091-4.2.6.1-1 | MUST | The content header carries the method's class id (60), weight 0, the body's size and property flags matching the properties that follow. | Amqp0MessagingTests.Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel |
| Amqp0:AMQP091-4.2.6.1-2 | MUST | A receiver reads every property the flags announce (such as content-type) before the body frames. | Amqp0MessagingTests.Get_GetOkWithAMessageInSeveralFrames_WritesTheBodyAndSendsNoAck |
| Amqp0:AMQP091-4.2.6.2-1 | MUST NOT | Content with an empty body has no body frames. | Amqp0MessagingTests.Publish_EmptyBody_SendsAHeaderOfSizeZeroAndNoBodyFrame |
| Amqp0:AMQP091-4.2.6.2-2 | MUST | A body larger than frame-max less the frame overhead is split into body frames that each fit. | Amqp0MessagingTests.Publish_OverFrameMax_SplitsTheBodyIntoFramesThatFit |
| Amqp0:AMQP091-4.2.6.2-3 | MUST | A receiver joins body frames until the header's body size is reached. | Amqp0MessagingTests.Get_GetOkWithAMessageInSeveralFrames_WritesTheBodyAndSendsNoAck |

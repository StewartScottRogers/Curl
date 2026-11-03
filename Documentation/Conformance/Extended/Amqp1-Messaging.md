# Amqp1: transfer, flow, disposition and the message format

Curl's `amqp1://` send (`-d`) and receive measured against OASIS AMQP Version 1.0 (29 October 2012): flow control,
link credit, transfer, multi-frame transfers, disposition and settlement (Part 2 sections 2.6.7 to 2.6.13 and 2.7),
the message format (Part 3 section 3.2), the delivery states (section 3.4) and source and target (section 3.5)
(BL-4467). Protocol headers, SASL, the connection, session and link are measured in [Amqp1.md](Amqp1.md).

The cases in `Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/` run against `Amqp1ReferenceContainer` with the
link behaviours of `Curl.Conformance.Extended.UnitLibrary/Amqp1/Messaging/`: `Amqp1ReferenceReceivingLink`, for a
send, holds curl at no link credit for a while, counts any transfer that arrives before the grant, and settles the
delivery with the outcome a case chooses; `Amqp1ReferenceSendingLink`, for a receive, answers curl's flow with the
transfer frames a case writes with `Amqp1ReferenceMessage`, or with none for a source that holds no message.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Amqp1:Core-2.6.7-1 | MUST NOT | A sender sends no transfer on a link while its link-credit is zero; it waits for a flow that grants credit. | Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends |
| Amqp1:Core-2.6.7-2 | MUST | A receiver grants link credit with a flow naming the link's handle before the sender can send it a message. | Amqp1ReceiveCases.Receive_UnsettledData_WritesItAndSettlesAccepted |
| Amqp1:Core-2.6.7-3 | MUST NOT | A receiver whose source holds no message writes no message and settles nothing. | Amqp1ReceiveCases.Receive_NoMessage_TimesOutWithExit28AndWritesNothing |
| Amqp1:Core-2.6.7-4 | MUST | A sender asked to drain uses up its credit and sends a flow when it has nothing more to send. | n/a: curl's sender is never asked to drain and its receiver never sets drain |
| Amqp1:Core-2.6.10-1 | MUST | A link is stopped by a flow setting link-credit to zero, and the peer stops sending on it. | n/a: carried by Amqp1:Core-2.6.7-1, whose case holds curl at zero credit |
| Amqp1:Core-2.6.12-1 | MUST | The first transfer of a delivery carries the attached handle, its delivery-id and a delivery-tag unique among the link's unsettled deliveries. | Amqp1SendCases.Send_SettledAccepted_OneDataSectionAndExit0 |
| Amqp1:Core-2.6.12-2 | SHOULD NOT | A receiver sends no disposition settling a delivery the sender already settled. | Amqp1ReceiveCases.Receive_SettledTwoDataSectionsInTwoTransfersWithBareMessageAround_WritesBothAndSendsNoDisposition |
| Amqp1:Core-2.6.12-3 | MUST NOT | Under rcv-settle-mode second, a receiver settles a delivery only after the sender has. | n/a: curl attaches with rcv-settle-mode first (the default) |
| Amqp1:Core-2.6.13-1 | MUST | On resuming a link, each side reconciles the unsettled map the other's attach carries. | n/a: curl attaches each link afresh with no unsettled map and never resumes one |
| Amqp1:Core-2.7.5-1 | MUST | A message larger than a frame is sent in several transfers, every one but the last with more set, all with the first's delivery-id or none, and the reader joins them in order. | Amqp1SendCases.Send_1500BytesAt512PerFrame_SplitWithMoreOnAllButTheLast |
| Amqp1:Core-2.7.5-2 | MUST | A frame on a handle that is not attached ends the session with the error amqp:session:unattached-handle. | Amqp1ReceiveCases.Receive_TransferOnUnattachedHandle_EndsTheSessionWithUnattachedHandle |
| Amqp1:Core-2.7.5-3 | MUST | A continuation transfer whose delivery-id differs from the first transfer's is an error, not part of the message. | Amqp1ReceiveCases.Receive_ContinuationChangesTheDeliveryId_FailsWithExit8 |
| Amqp1:Core-2.7.6-1 | MUST | A receiver settles a delivery with a disposition whose role is receiver, whose first is the delivery-id, with settled set and the outcome as its state. | Amqp1ReceiveCases.Receive_UnsettledData_WritesItAndSettlesAccepted |
| Amqp1:Messaging-3.2-1 | MUST | A bare message has a body of one or more data sections, one amqp-sequence or one amqp-value. | Amqp1SendCases.Send_SettledAccepted_OneDataSectionAndExit0 |
| Amqp1:Messaging-3.2-2 | MUST | A reader takes header, properties and application-properties before the body and a footer after it, and only the body is the message's content. | Amqp1ReceiveCases.Receive_SettledTwoDataSectionsInTwoTransfersWithBareMessageAround_WritesBothAndSendsNoDisposition |
| Amqp1:Messaging-3.2.6-1 | MUST | A data section's binary is the message's content; several data sections are its content in order. | Amqp1ReceiveCases.Receive_UnsettledData_WritesItAndSettlesAccepted |
| Amqp1:Messaging-3.2.8-1 | MUST | An amqp-value section carries one AMQP value as the body. | Amqp1ReceiveCases.Receive_AmqpValueString_WritesTheString |
| Amqp1:Messaging-3.4.2-1 | MUST | The accepted outcome means the receiver processed the message: the send succeeded. | Amqp1SendCases.Send_SettledAccepted_OneDataSectionAndExit0 |
| Amqp1:Messaging-3.4.3-1 | MUST | The rejected outcome means the message is invalid, and its error says why: the send failed. | Amqp1SendCases.Send_Rejected_FailsWithExit55AndTheError |
| Amqp1:Messaging-3.4.4-1 | MUST | The released outcome means the message was not processed: the send did not succeed. | Amqp1SendCases.Send_Released_FailsWithExit55 |
| Amqp1:Messaging-3.4.5-1 | MUST | The modified outcome means the message was not processed and is to be modified: the send did not succeed. | Amqp1SendCases.Send_Modified_FailsWithExit55 |
| Amqp1:Messaging-3.5.3-1 | MUST | A receiving link's source names the address to receive from. | Amqp1ReceiveCases.Receive_UnsettledData_WritesItAndSettlesAccepted |
| Amqp1:Messaging-3.5.4-1 | MUST | A sending link's target names the address to send to. | Amqp1SendCases.Send_SettledAccepted_OneDataSectionAndExit0 |
| Amqp1:Messaging-3.5.3-2 | MUST | A link that asks for a dynamic node sets dynamic and leaves the address empty. | n/a: curl always names the address from the URL and never asks for a dynamic node |

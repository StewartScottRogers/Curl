# AMQP 0-9-1: protocol header, framing, data types and the connection class

Curl's `amqp://` and `amqps://` client (`Curl.Extended.Protocol.Amqp0.UnitLibrary`) against the AMQP 0-9-1
specification (2008-11-13) with RabbitMQ's errata, part 1 of 2 (BL-4464). Channels, `queue.declare`, the basic
class and content frames are in [Amqp0-Messaging.md](Amqp0-Messaging.md) (BL-4465). The cases are in
`Curl.Conformance.Extended.UnitTests/Amqp0/` and run against `Amqp0ReferenceBroker`
(`Curl.Conformance.Extended.UnitLibrary/Amqp0/`), which a later part extends through `Amqp0MethodTable.With`.
`AMQP091-<section>-<n>` locates a requirement in the specification's section; `1.4.2.x` is the connection
class's method section of the XML specification's documentation.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Amqp0:AMQP091-4.2.2-1 | MUST | The client opens the connection by sending the protocol header "AMQP" %d0 %d0 %d9 %d1. | Amqp0ConnectionTests.Publish_OpensWithTheProtocolHeaderAndClosesTheConnection |
| Amqp0:AMQP091-4.2.2-2 | MUST | A server that refuses the version answers with its own protocol header and closes; the client then sends no frames and fails. | Amqp0ConnectionTests.ProtocolHeader_BrokerAnswersWithAnotherVersion_FailsWithoutFrames |
| Amqp0:AMQP091-4.2.3-1 | MUST | Every frame ends with the frame-end octet %xCE. | Amqp0ConnectionTests.Publish_OpensWithTheProtocolHeaderAndClosesTheConnection |
| Amqp0:AMQP091-4.2.3-2 | MUST | A frame whose frame-end octet is not %xCE is a fatal frame error. | Amqp0ConnectionTests.Frame_WithoutFrameEnd_IsAFrameError |
| Amqp0:AMQP091-4.2.3-3 | MUST | A frame of an unknown type is a fatal frame error. | Amqp0ConnectionTests.Frame_OfAnUnknownType_IsAFrameError |
| Amqp0:AMQP091-4.2.3-4 | MUST | A peer that reads a malformed frame signals a connection exception with reply code 501 (frame-error): connection.close, then close the socket. | Amqp0ConnectionTests.Frame_Malformed_IsAnsweredWithConnectionClose501 |
| Amqp0:AMQP091-4.2.3-5 | MUST NOT | A peer sends no frame larger than the negotiated frame-max; content bodies are split into frames that fit. | Amqp0ConnectionTests.Tune_ClientAcceptsLowerLimitsAndSplitsContentAtFrameMax |
| Amqp0:AMQP091-4.2.5.5-1 | MUST | Field names in a field table start with a letter, '$' or '#' and are at most 128 characters; values carry a type octet (client-properties of start-ok). | Amqp0ConnectionTests.StartOk_ChoosesPlainAndAnOfferedLocaleInAWellFormedTable |
| Amqp0:AMQP091-4.2.7-1 | MUST | A peer accepts heartbeat frames at any time after tuning and does not treat them as methods. | Amqp0ConnectionTests.Heartbeat_ClientDeclinesAndSkipsHeartbeatFrames |
| Amqp0:AMQP091-4.2.7-2 | MAY | The client declines heartbeats by answering tune-ok with heartbeat 0. | Amqp0ConnectionTests.Heartbeat_ClientDeclinesAndSkipsHeartbeatFrames |
| Amqp0:AMQP091-4.2.7-3 | MUST | Heartbeat frames use channel 0; one on another channel is a frame error. | Amqp0ConnectionTests.Heartbeat_OnAChannelOtherThanZero_IsAFrameError |
| Amqp0:AMQP091-2.2.4-1 | MUST | The client closes the connection with connection.close on channel 0 (reply-success 200) and waits for close-ok. | Amqp0ConnectionTests.Publish_OpensWithTheProtocolHeaderAndClosesTheConnection |
| Amqp0:AMQP091-1.4.2.1-1 | MUST | The client answers connection.start with start-ok before any other method. | Amqp0ConnectionTests.StartOk_ChoosesPlainAndAnOfferedLocaleInAWellFormedTable |
| Amqp0:AMQP091-1.4.2.2-1 | MUST | start-ok's mechanism and locale are ones connection.start offered. | Amqp0ConnectionTests.StartOk_ChoosesPlainAndAnOfferedLocaleInAWellFormedTable |
| Amqp0:AMQP091-1.4.2.2-2 | MUST | A client that supports none of the offered mechanisms does not answer with another mechanism. | Amqp0ConnectionTests.Start_WithoutPlain_FailsLoginWithoutAnswering |
| Amqp0:AMQP091-1.4.2.3-1 | MUST | The client answers connection.secure with secure-ok. | n/a: the client chooses PLAIN, whose credentials travel in start-ok's response and which has no challenge, so no broker sends connection.secure to it |
| Amqp0:AMQP091-1.4.2.6-1 | MUST | tune-ok's channel-max does not exceed the server's proposal. | Amqp0ConnectionTests.Tune_ClientAcceptsLowerLimitsAndSplitsContentAtFrameMax |
| Amqp0:AMQP091-1.4.2.6-2 | MUST | tune-ok's frame-max does not exceed the server's proposal. | Amqp0ConnectionTests.Tune_ClientAcceptsLowerLimitsAndSplitsContentAtFrameMax |
| Amqp0:AMQP091-1.4.2.6-3 | MUST | A proposal of zero means no limit, so the client sends its own limits. | Amqp0ConnectionTests.Tune_ZeroMeansNoLimitSoTheClientProposesItsOwn |
| Amqp0:AMQP091-1.4.2.8-1 | MUST | The client sends tune-ok, then connection.open naming the virtual host, before any channel method. | Amqp0ConnectionTests.Publish_OpensWithTheProtocolHeaderAndClosesTheConnection |
| Amqp0:AMQP091-1.4.2.9-1 | MUST | A peer that receives connection.close answers with close-ok and sends nothing else. | Amqp0ConnectionTests.Close_FromTheBroker_IsAnsweredWithCloseOk |
| Amqp0:AMQP091-1.4.2.9-2 | SHOULD | A connection.close with 403 (access-refused) is reported as a refused login. | Amqp0ConnectionTests.Close_AccessRefused_FailsLogin |
| Amqp0:RFC4616-2-1 | MUST | The PLAIN response is NUL authcid NUL password (RFC 4616); without credentials the client uses guest/guest. | Amqp0ConnectionTests.StartOk_ChoosesPlainAndAnOfferedLocaleInAWellFormedTable |
| Amqp0:RFC4616-2-2 | MUST | The PLAIN response carries the credentials of `-u` and of the URL's userinfo. | Amqp0ConnectionTests.StartOk_CarriesTheCredentialsOfTheUserOptionAndOfTheUrl |
| Amqp0:RabbitMQ-URI-amqps-1 | MUST | `amqps://` runs the same connection over TLS (port 5671). | Amqp0ConnectionTests.Amqps_RunsTheSameConnectionOverTls |

## What `-T` does

`-T file` (BL-4956) publishes the file's bytes exactly as `-d` publishes its data: the client declares the queue and
sends basic.publish to the default exchange with the file as the content body, and never sends basic.get, so it
neither fetches nor consumes a message. When both `-d` and `-T` are given, `-d`'s data is published, as on
`amqp1://`. Pinned by `Amqp0UploadTests` in `Curl.Extended.Protocol.Amqp0.UnitTests`.

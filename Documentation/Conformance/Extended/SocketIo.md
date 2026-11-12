# SocketIo: Engine.IO v4 and Socket.IO v5 over the WebSocket transport

Curl's `socketio://` and `socketios://` client (`Curl.Extended.Protocol.SocketIo.UnitLibrary`, ADR-1035) measured
against the Engine.IO protocol v4 (`EIO-`, github.com/socketio/engine.io-protocol, v4 branch), the Socket.IO protocol
v5 (`SIO-`, github.com/socketio/socket.io-protocol, v5 branch) and the RFC 6455 client rules the WebSocket transport
relies on (BL-4492). Only requirements that bind a client are rows. The reference server in
`Curl.Conformance.Extended.UnitLibrary/SocketIo/` answers the WebSocket upgrade from RFC 6455 and plays each case's
scripted Engine.IO packets; it references no `Curl.Extended.Protocol.*` project. Cases listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` fail today and name the task that fixes them.

| ID | Level | Requirement | Covered by |
|----|:-----:|-------------|------------|
| SocketIo:EIO-Transport-1 | MUST | A WebSocket-only connection opens with a GET to `/socket.io/?EIO=4&transport=websocket`, the protocol version in `EIO`. | SocketIoEngineIoCases.Upgrade_IsAWebSocketOnlyConnectionWithMaskedTextFrames |
| SocketIo:EIO-Transport-2 | MUST | A secure endpoint opens the same WebSocket over TLS (`wss`). | SocketIoEngineIoCases.SecureScheme_UpgradesOverTls |
| SocketIo:EIO-Transport-3 | MUST | The HTTP long-polling transport's request rules, and the `upgrade` (5) packet that moves a polling session to WebSocket with the `2probe`/`3probe` exchange. | n/a: Curl opens the WebSocket transport directly and never polls (ADR-1035) |
| SocketIo:EIO-Open-1 | MUST | The server's first packet is `open` (0) with `sid`, `upgrades`, `pingInterval`, `pingTimeout` and `maxPayload`; the client reads it before sending. | SocketIoEngineIoCases.OpenPacket_IsReadBeforeTheClientSendsItsConnect |
| SocketIo:EIO-Open-2 | MUST | On the WebSocket transport each packet is one WebSocket message, its type the first character. | SocketIoEngineIoCases.OpenPacket_IsReadBeforeTheClientSendsItsConnect |
| SocketIo:EIO-Heartbeat-1 | MUST | In v4 the server sends `ping` (2) and the client answers with `pong` (3). | SocketIoEngineIoCases.ServerPing_IsAnsweredWithAPong |
| SocketIo:EIO-Heartbeat-2 | MUST | The client considers the connection closed when no ping arrives within `pingInterval` + `pingTimeout`. | SocketIoEngineIoCases.MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock |
| SocketIo:EIO-Close-1 | MUST | A `close` (1) packet ends the session. | SocketIoEngineIoCases.NoopIsIgnoredAndCloseEndsTheSession |
| SocketIo:EIO-Noop-1 | MUST | A `noop` (6) packet carries nothing and is ignored. | SocketIoEngineIoCases.NoopIsIgnoredAndCloseEndsTheSession |
| SocketIo:EIO-MaxPayload-1 | MUST | The client sends no message larger than the `maxPayload` of the handshake. | SocketIoEngineIoCases.EventOverMaxPayload_IsNotSent |
| SocketIo:EIO-Binary-1 | MAY | Binary data travels in binary WebSocket frames with no type prefix. | n/a: Curl sends only text packets; the `-d` event array is JSON (ADR-1035) |
| SocketIo:SIO-Encoding-1 | MUST | A packet is its type digit, then `/namespace,` unless the namespace is `/`, then the ack ID, then the JSON payload. | SocketIoPacketCases.ConnectToTheMainNamespace_SendsCONNECTAndWaitsForItsAnswer |
| SocketIo:SIO-Encoding-2 | MUST | A packet for another namespace is not a packet of the client's namespace. | SocketIoPacketCases.ConnectToANamedNamespace_SendsItAndReadsOnlyItsPackets |
| SocketIo:SIO-Connect-1 | MUST | After `open` the client sends CONNECT (0) for its namespace, the main one as `40`. | SocketIoPacketCases.ConnectToTheMainNamespace_SendsCONNECTAndWaitsForItsAnswer |
| SocketIo:SIO-Connect-2 | MUST | A named namespace is sent in the CONNECT, as `40/admin,`. | SocketIoPacketCases.ConnectToANamedNamespace_SendsItAndReadsOnlyItsPackets |
| SocketIo:SIO-Connect-3 | MUST | The client sends no EVENT before the server's CONNECT answer with `sid`. | SocketIoPacketCases.ConnectToTheMainNamespace_SendsCONNECTAndWaitsForItsAnswer |
| SocketIo:SIO-Connect-4 | MUST | A session that ends before the CONNECT is answered is a failed connection. | SocketIoPacketCases.ServerClosingBeforeTheConnectIsAccepted_FailsWithExit52 |
| SocketIo:SIO-Connect-5 | MAY | The CONNECT may carry an authentication payload. | n/a: Curl sends no auth payload; the URL and `-d` carry no field for one (ADR-1035) |
| SocketIo:SIO-ConnectError-1 | MUST | A CONNECT_ERROR (4) refuses the namespace; the client is not connected. | SocketIoPacketCases.ConnectError_FailsWithExit22AndTheServersMessage |
| SocketIo:SIO-Event-1 | MUST | An EVENT (2) the client emits is a JSON array whose first element is the event name. | SocketIoPacketCases.EmittedEvent_IsSentAfterTheConnectIsAccepted |
| SocketIo:SIO-Event-2 | MUST | An EVENT received carries its name and arguments as a JSON array. | SocketIoPacketCases.ReceivedEvents_AreEachWrittenAsTheirArray |
| SocketIo:SIO-Ack-1 | MUST | An EVENT with an ack ID is answered with an ACK (3) carrying that ID. | SocketIoPacketCases.EventWithAnAckId_IsAnsweredWithAnAckCarryingThatId |
| SocketIo:SIO-Ack-2 | MAY | The client may ask for an acknowledgement of its own EVENT. | n/a: Curl's emitted event carries no ack ID, so no ACK answers it (ADR-1035) |
| SocketIo:SIO-Disconnect-1 | MUST | A DISCONNECT (1) from the server ends the client's namespace session. | SocketIoPacketCases.DisconnectFromTheServer_EndsTheTransferWithExit0 |
| SocketIo:SIO-Binary-1 | MUST | A BINARY_EVENT (5) or BINARY_ACK (6) is followed by its attachments, which are part of the packet and not packets of their own. | SocketIoPacketCases.BinaryEventAndItsAttachment_AreSkippedAndTheSessionGoesOn |
| SocketIo:SIO-Binary-2 | MUST | The attachments replace the `_placeholder` objects of the payload. | n/a: Curl writes JSON events only and skips binary packets (ADR-1035) |
| SocketIo:RFC6455-4.1-1 | MUST | The opening handshake is a GET with `Host`, `Upgrade: websocket`, `Connection: Upgrade`, a 16-byte base64 `Sec-WebSocket-Key` and `Sec-WebSocket-Version: 13`. | SocketIoEngineIoCases.Upgrade_IsAWebSocketOnlyConnectionWithMaskedTextFrames |
| SocketIo:RFC6455-4.1-2 | MUST | The client fails the connection when `Sec-WebSocket-Accept` does not answer its key. | SocketIoEngineIoCases.UpgradeAnsweredWithTheWrongAccept_FailsWithExit8AndSendsNoFrame |
| SocketIo:RFC6455-5.1-1 | MUST | The client masks every frame it sends. | SocketIoEngineIoCases.Upgrade_IsAWebSocketOnlyConnectionWithMaskedTextFrames |
| SocketIo:RFC6455-5.6-1 | MUST | Engine.IO text packets travel in text frames. | SocketIoEngineIoCases.Upgrade_IsAWebSocketOnlyConnectionWithMaskedTextFrames |
| SocketIo:RFC6455-5.4-1 | MUST | A message fragmented over a text frame and continuations is reassembled. | SocketIoEngineIoCases.FragmentedTextMessage_IsReassembled |
| SocketIo:RFC6455-5.5.2-1 | MUST | A Ping is answered with a Pong carrying its payload. | SocketIoEngineIoCases.WebSocketPing_IsAnsweredWithAPongCarryingItsPayload |
| SocketIo:RFC6455-5.5.1-1 | MUST | A Close received is answered with a Close. | SocketIoEngineIoCases.WebSocketClose_IsAnsweredWithAClose |

## What `-T` does

`-T file` sends the file as the event exactly as `-d` does, and does not change the namespace: the file name the core appends to a URL ending in `/` is cut off again, so `socketio://host/` stays the main namespace. When both are given, `-d` wins (BL-5020). Pinned by `SocketIoProtocolHandlerTests.ExecuteAsync_AnUploadToANamedNamespace_SendsTheFileAsTheEvent`, `ExecuteAsync_AnUploadWhoseFileNameWasAppendedToTheURL_KeepsTheMainNamespace` and `ExecuteAsync_AnUploadAndPostData_SendsThePostData` in `Curl.Extended.Protocol.SocketIo.UnitTests`.

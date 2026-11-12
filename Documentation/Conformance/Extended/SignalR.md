# SignalR: JSON hub protocol, negotiate and the WebSocket transport

Curl's `signalr://` and `signalrs://` client (`Curl.Extended.Protocol.SignalR.UnitLibrary`) measured against ASP.NET
Core SignalR's specifications at dotnet/aspnetcore `main` (the library cites no tag): `src/SignalR/docs/specs/HubProtocol.md`
(the JSON encoding), `src/SignalR/docs/specs/TransportProtocols.md` (negotiate and the WebSockets transport) and the
RFC 6455 client rules the transport relies on (BL-4491). Only requirements that bind a client are rows. The reference
hub in `Curl.Conformance.Extended.UnitLibrary/SignalR/` answers negotiate and the WebSocket upgrade from the
specifications and plays each case's scripted frames; it references no `Curl.Extended.Protocol.*` project. Cases
listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` fail today and name the task that fixes them.

| ID | Level | Requirement | Covered by |
|----|:-----:|-------------|------------|
| SignalR:TP-Negotiate-1 | MUST | The client starts with `POST [endpoint-base]/negotiate`, asking for `negotiateVersion=1` in the query. | SignalRTransportCases.NegotiateVersion1_PostsNegotiateThenConnectsWithTheConnectionToken |
| SignalR:TP-Negotiate-2 | MUST | Under negotiate version 1 the client connects with the `connectionToken` as the `id`. | SignalRTransportCases.NegotiateVersion1_PostsNegotiateThenConnectsWithTheConnectionToken |
| SignalR:TP-Negotiate-3 | MUST | A version 0 response (no `negotiateVersion`) has no token; the client connects with the `connectionId` as the `id`. | SignalRTransportCases.NegotiateVersion0_ConnectsWithTheConnectionId |
| SignalR:TP-Negotiate-4 | MUST | A response with `url` and `accessToken` redirects: the client negotiates again at `url`. | SignalRTransportCases.NegotiateRedirect_NegotiatesAgainAtTheUrlWithTheAccessTokenAndConnectsThere |
| SignalR:TP-Negotiate-5 | MUST | After a redirect the client sends the `accessToken` as a bearer token on every later request, the upgrade included. | SignalRTransportCases.NegotiateRedirect_NegotiatesAgainAtTheUrlWithTheAccessTokenAndConnectsThere |
| SignalR:TP-Negotiate-6 | MUST | A response with `error` ends the connection attempt; no transport is opened. | SignalRTransportCases.NegotiateError_FailsWithExit8AndOpensNoWebSocket |
| SignalR:TP-Negotiate-7 | MUST | The client uses only a transport `availableTransports` offers. | SignalRTransportCases.NegotiateOffersNoWebSockets_FailsWithExit8 |
| SignalR:TP-Negotiate-8 | MAY | The client may skip negotiate and connect straight to the WebSockets transport. | n/a: an option, not a requirement; Curl always negotiates |
| SignalR:TP-WebSockets-1 | MUST | The WebSockets transport connects to the endpoint with `ws` for `http` and the `id` query parameter. | SignalRTransportCases.NegotiateVersion1_PostsNegotiateThenConnectsWithTheConnectionToken |
| SignalR:TP-WebSockets-2 | MUST | An `https` endpoint uses `wss`, so negotiate and the WebSocket both run over TLS. | SignalRTransportCases.SecureScheme_NegotiatesAndConnectsOverTls |
| SignalR:TP-LongPolling-1 | MUST | The Server-Sent Events and Long Polling transports' request rules. | n/a: Curl implements only the WebSockets transport (ADR-1084) |
| SignalR:HUB-Handshake-1 | MUST | The client's first message is the handshake request `{"protocol":"json","version":1}`. | SignalRHubProtocolCases.Invocation_SendsHandshakeThenInvocationAndWritesTheCompletionResult |
| SignalR:HUB-Handshake-2 | MUST | The client sends no other message before the handshake response. | SignalRHubProtocolCases.HandshakeError_FailsWithExit22AndSendsNoCall |
| SignalR:HUB-Handshake-3 | MUST | A handshake response with `error` fails the connection. | SignalRHubProtocolCases.HandshakeError_FailsWithExit22AndSendsNoCall |
| SignalR:HUB-Handshake-4 | MUST | Messages that follow the handshake response in the same frame are read as messages. | SignalRHubProtocolCases.RecordsSplitAcrossFramesAndSeveralInOneFrame_AreEachRead |
| SignalR:HUB-Json-1 | MUST | Each JSON message the client sends is a JSON object ended by the record separator 0x1E. | SignalRHubProtocolCases.Invocation_SendsHandshakeThenInvocationAndWritesTheCompletionResult |
| SignalR:HUB-Json-2 | MUST | Records are cut at 0x1E, whatever the transport's frames: one may span frames and a frame may hold several. | SignalRHubProtocolCases.RecordsSplitAcrossFramesAndSeveralInOneFrame_AreEachRead |
| SignalR:HUB-Json-3 | MUST | A record that is not a hub message is a protocol error. | SignalRHubProtocolCases.RecordThatIsNotAHubMessage_FailsWithExit8 |
| SignalR:HUB-Invocation-1 | MUST | An Invocation (type 1) carries `target`, `arguments` and, when a result is expected, a unique `invocationId`. | SignalRHubProtocolCases.Invocation_SendsHandshakeThenInvocationAndWritesTheCompletionResult |
| SignalR:HUB-Invocation-2 | MUST NOT | An Invocation with no `invocationId` (non-blocking) is never answered with a Completion. | SignalRHubProtocolCases.HubCallFromTheServer_WritesItAsAnArrayAndSendsNoCompletion |
| SignalR:HUB-Invocation-3 | MUST | An Invocation with an `invocationId` is answered with a Completion carrying that id. | SignalRHubProtocolCases.HubCallWithAnInvocationId_IsAnsweredWithACompletion |
| SignalR:HUB-Completion-1 | MUST | A Completion (type 3) with `result` ends the invocation of its `invocationId` with that result. | SignalRHubProtocolCases.Invocation_SendsHandshakeThenInvocationAndWritesTheCompletionResult |
| SignalR:HUB-Completion-2 | MUST | A Completion with `error` ends the invocation with that error. | SignalRHubProtocolCases.CompletionWithError_FailsWithExit22AndTheError |
| SignalR:HUB-Completion-3 | MUST | A Completion with neither `result` nor `error` ends a void invocation. | SignalRHubProtocolCases.CompletionWithNeitherResultNorError_EndsWithNoLine |
| SignalR:HUB-Stream-1 | MUST | A StreamInvocation (type 4) carries `invocationId`, `target` and `arguments`. | SignalRHubProtocolCases.StreamInvocation_WritesEachItemAndEndsAtItsCompletion |
| SignalR:HUB-Stream-2 | MUST | StreamItems (type 2) of the invocation are its items, in order, until its Completion; others are not. | SignalRHubProtocolCases.StreamInvocation_WritesEachItemAndEndsAtItsCompletion |
| SignalR:HUB-Stream-3 | MUST | Client-to-server streams: `streamIds` on an invocation, then the client's StreamItems and Completion. | n/a: Curl's `-d` call sends no stream arguments |
| SignalR:HUB-Cancel-1 | MUST | A CancelInvocation (type 5) names a stream the client no longer wants. | n/a: Curl reads every stream it starts to its Completion, so it never cancels |
| SignalR:HUB-Ping-1 | MUST | A Ping (type 6) needs no answer and changes nothing. | SignalRHubProtocolCases.Ping_IsIgnoredAndTheCallCompletes |
| SignalR:HUB-Ping-2 | SHOULD | Each side sends a Ping when it has sent nothing for its keep-alive interval. | n/a: a keep-alive interval (15 s by default) is the client's configuration; a curl run is one call |
| SignalR:HUB-Close-1 | MUST | A Close (type 7) without `error` ends the connection normally. | SignalRHubProtocolCases.HubCallFromTheServer_WritesItAsAnArrayAndSendsNoCompletion |
| SignalR:HUB-Close-2 | MUST | A Close with `error` ends the connection with that error. | SignalRHubProtocolCases.CloseWithError_FailsWithExit22AndTheError |
| SignalR:HUB-Close-3 | MAY | `allowReconnect` lets the client reconnect. | n/a: Curl never reconnects; a run is one connection |
| SignalR:HUB-MessagePack-1 | MUST | The MessagePack encoding's rules. | n/a: Curl speaks only the JSON encoding (ADR-1032) |
| SignalR:RFC6455-4.1-1 | MUST | The opening handshake is a GET with `Host`, `Upgrade: websocket`, `Connection: Upgrade`, a 16-byte base64 `Sec-WebSocket-Key` and `Sec-WebSocket-Version: 13`. | SignalRWebSocketCases.OpeningHandshakeAndFrames_AreAClientsMaskedTextFrames |
| SignalR:RFC6455-4.1-2 | MUST | The client fails the connection when `Sec-WebSocket-Accept` does not answer its key. | SignalRWebSocketCases.UpgradeAnsweredWithTheWrongAccept_FailsWithExit8AndSendsNoFrame |
| SignalR:RFC6455-5.1-1 | MUST | The client masks every frame it sends. | SignalRWebSocketCases.OpeningHandshakeAndFrames_AreAClientsMaskedTextFrames |
| SignalR:RFC6455-5.6-1 | MUST | The JSON hub protocol travels in text frames. | SignalRWebSocketCases.OpeningHandshakeAndFrames_AreAClientsMaskedTextFrames |
| SignalR:RFC6455-5.4-1 | MUST | A message fragmented over a text frame and continuations is reassembled. | SignalRWebSocketCases.FragmentedTextMessage_IsReassembled |
| SignalR:RFC6455-5.5.2-1 | MUST | A Ping is answered with a Pong carrying its payload. | SignalRWebSocketCases.WebSocketPing_IsAnsweredWithAPongCarryingItsPayload |
| SignalR:RFC6455-5.5.1-1 | MUST | A Close received is answered with a Close. | SignalRWebSocketCases.WebSocketClose_IsAnsweredWithAClose |
| SignalR:RFC6455-1.4-1 | SHOULD | The client ends the WebSocket with a Close frame, not by dropping the TCP connection. | SignalRWebSocketCases.CallCompleted_ClosesTheWebSocketWithACloseFrame |
| SignalR:CURL-Upload-1 | MAY | `-T file` sends the file as the invocation exactly as `-d` does; `-d` wins when both are given. | n/a: pinned by SignalRProtocolHandlerTests.Upload_IsSentAsTheInvocationExactlyAsDataWould and SignalRProtocolHandlerTests.DataAndUpload_DataWins in Curl.Extended.Protocol.SignalR.UnitTests, outside this harness |

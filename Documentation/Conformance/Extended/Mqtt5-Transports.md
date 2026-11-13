# MQTT 5 requirement matrix: WebSocket (chapter 6) and TLS (chapter 5)

Curl's `mqtt5+ws://` and `mqtt5s://` transports (`Curl.Extended.Protocol.Mqtt5.UnitLibrary`, `WebSocket/` and
`Client/`) measured against OASIS MQTT Version 5.0 (7 March 2019) chapter 6 (WebSocket as a network transport), the
RFC 6455 client rules the handler relies on (section 4.1, the opening handshake; sections 5.1 to 5.5, framing) and the
chapter 5 client use of TLS, through the real curl command line (BL-4486). The cases in
`Curl.Conformance.Extended.UnitTests/Mqtt5/Transports/` run curl against
`Mqtt5/Transports/Mqtt5WebSocketReferenceServer` (an RFC 6455 server written from the RFC, carrying the MQTT stream to
scripted answers) and, for TLS, against the reference broker on a TLS route or `Mqtt5/Transports/Mqtt5TlsRefusalServer`.
Chapter 6's statements carry the specification's IDs; an RFC 6455 rule has a `RFC6455-<section>-<n>` ID and a binding
chapter 5 sentence without a number a `MQTT5-<section>-<n>` ID of its own.

curl offers only the `mqtt` subprotocol to `/mqtt` (ADR-1054), negotiates no extension and sends no Close or Ping of
its own; the statements binding a server, an extension or a fragmenting sender are `n/a`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Mqtt5:MQTT-6.0.0-1 | MUST | MQTT packets go in binary data frames; a recipient of any other data frame closes the connection. | WebSocketCases.Frames_TextFrameFromTheServer_ClosesTheConnection |
| Mqtt5:MQTT-6.0.0-2 | MUST NOT | The receiver does not assume MQTT packets are aligned on frame boundaries. | WebSocketCases.Frames_PacketSplitOverFramesAndPacketsSharingAFrame_AreReadAsOneStream |
| Mqtt5:MQTT-6.0.0-3 | MUST | The client includes `mqtt` in the subprotocols it offers. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:MQTT-6.0.0-4 | MUST | The subprotocol the server selects is `mqtt`; curl refuses any other. | WebSocketCases.Handshake_ReplyWithoutTheMqttSubprotocol_FailsBeforeAnyFrameWithExit8 |
| Mqtt5:RFC6455-4.1-1 | MUST | The handshake is a valid HTTP/1.1 GET of the resource name. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-2 | MUST | The request has a Host header field naming the host (and a non-default port). | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-3 | MUST | The request has `Upgrade: websocket`. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-4 | MUST | The request has `Connection: Upgrade`. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-5 | MUST | Sec-WebSocket-Key is a base64 nonce of 16 bytes. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-6 | MUST | The request has `Sec-WebSocket-Version: 13`. | WebSocketCases.Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol |
| Mqtt5:RFC6455-4.1-7 | MAY | The request may have an Origin header field. | n/a: curl is no browser client and sends none |
| Mqtt5:RFC6455-4.1-8 | MUST | A status other than 101 is handled as HTTP; curl fails the upgrade with exit 22. | WebSocketCases.Handshake_NotSwitchingProtocols_FailsBeforeAnyFrameWithExit22 |
| Mqtt5:RFC6455-4.1-9 | MUST | A reply without `Upgrade: websocket` fails the WebSocket connection (BL-4593). | WebSocketCases.Handshake_ReplyWithoutUpgradeWebsocket_FailsBeforeAnyFrame |
| Mqtt5:RFC6455-4.1-10 | MUST | A reply without `Connection: Upgrade` fails the WebSocket connection (BL-4593). | WebSocketCases.Handshake_ReplyWithoutConnectionUpgrade_FailsBeforeAnyFrame |
| Mqtt5:RFC6455-4.1-11 | MUST | A Sec-WebSocket-Accept other than the key's derived value fails the connection. | WebSocketCases.Handshake_WrongAccept_FailsBeforeAnyFrameWithExit8 |
| Mqtt5:RFC6455-4.1-12 | MUST | A reply selecting no offered subprotocol, or one not offered, fails the connection. | WebSocketCases.Handshake_ReplyWithoutTheMqttSubprotocol_FailsBeforeAnyFrameWithExit8 |
| Mqtt5:RFC6455-4.1-13 | MUST | A reply with an extension the client did not offer fails the connection. | n/a: curl offers no extension and the handler reads no Sec-WebSocket-Extensions; an extension's frames then break the RSV rule below |
| Mqtt5:RFC6455-5.1-1 | MUST | The client masks every frame it sends. | WebSocketCases.Frames_EveryClientFrameIsAMaskedFinalBinaryFrameWithAFreshKey |
| Mqtt5:RFC6455-5.1-2 | MUST | A client that receives a masked frame closes the connection. | WebSocketCases.Frames_MaskedFrameFromTheServer_ClosesTheConnection |
| Mqtt5:RFC6455-5.2-1 | MUST | RSV1 to RSV3 are 0 without a negotiated extension. | WebSocketCases.Frames_EveryClientFrameIsAMaskedFinalBinaryFrameWithAFreshKey |
| Mqtt5:RFC6455-5.2-2 | MUST | A frame with a non-zero RSV bit and no extension fails the connection. | WebSocketCases.Frames_ReservedBitSetByTheServer_ClosesTheConnection |
| Mqtt5:RFC6455-5.3-1 | MUST | Each frame's masking key is a fresh, unpredictable value. | WebSocketCases.Frames_EveryClientFrameIsAMaskedFinalBinaryFrameWithAFreshKey |
| Mqtt5:RFC6455-5.4-1 | MUST | A fragmented message is a first frame with FIN clear, continuation frames and a final one with FIN set. | WebSocketCases.Frames_PacketSplitOverFramesAndPacketsSharingAFrame_AreReadAsOneStream |
| Mqtt5:RFC6455-5.4-2 | MUST NOT | Control frames are not fragmented and carry at most 125 bytes. | n/a: binds the sender; curl's only control frame is a Pong echoing a Ping's payload |
| Mqtt5:RFC6455-5.5.1-1 | MUST | An endpoint that receives a Close frame sends a Close frame in response (BL-4594). | WebSocketCases.Frames_CloseFromTheServer_IsAnsweredWithACloseFrame |
| Mqtt5:RFC6455-5.5.1-2 | MUST NOT | No data frame is sent after a Close frame. | n/a: curl sends no Close frame of its own (see 5.5.1-1) |
| Mqtt5:RFC6455-5.5.2-1 | MUST | An endpoint that receives a Ping sends a Pong with the same payload. | WebSocketCases.Frames_PingFromTheServer_IsAnsweredWithAMaskedPongCarryingItsPayload |
| Mqtt5:MQTT5-5.4.5-1 | SHOULD | A client authenticates the server with TLS: curl refuses a certificate it cannot verify with exit 60. | TlsCases.Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 |
| Mqtt5:MQTT5-5.4.5-2 | MAY | The MQTT exchange runs unchanged inside TLS; `-k` skips the verification. | TlsCases.Mqtt5s_InsecureFlag_RunsTheSameMqttExchangeOverTlsOnPort8883 |
| Mqtt5:MQTT5-4.2-1 | SHOULD | MQTT over TLS uses the IANA port 8883 by default. | TlsCases.Mqtt5s_InsecureFlag_RunsTheSameMqttExchangeOverTlsOnPort8883 |
| Mqtt5:MQTT5-5.4.5-3 | MAY | A client presents its own certificate to authenticate itself. | n/a: the reference route requests no client certificate; `--cert` is the transport's, not this handler's |
| Mqtt5:CURL-WS-HEADERS-1 | MAY | The upgrade GET carries `-H` headers, the `-A` User-Agent (curl's own when absent) and the `-b` Cookie, as `ws://`, `socketio://` and `signalr://` do; a `-H` naming a field the upgrade sets replaces it (BL-5030). | WebSocketCases.Handshake_HeaderUserAgentAndCookieOptions_AreSentOnTheUpgrade |

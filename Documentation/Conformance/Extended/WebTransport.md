# WebTransport requirement matrix (draft-ietf-webtrans-http3-16)

Curl's `webtransport://` client (`Curl.Extended.Protocol.WebTransport.UnitLibrary`, ADR-1089) measured against
WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16, cited as `WTH3`) and the parts of RFC 9220, RFC 9297 and
RFC 9221 a session rests on, by the cases in `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/`. Each case runs
curl in process against `WebTransportReferenceServer` in `Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/`:
an HTTP/3 server on `Curl.Conformance.Http3Server.UnitLibrary`, written from the draft, that sends the SETTINGS a case
scripts (`SETTINGS_ENABLE_CONNECT_PROTOCOL`, `SETTINGS_H3_DATAGRAM`, `SETTINGS_WT_ENABLED`), answers the extended
CONNECT with the scripted status, echoes streams and datagrams, sends `WT_CLOSE_SESSION` when scripted, and records
what the client sent. Only requirements that bind a client are rows; a row whose case fails is listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with its fix task.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| WebTransport:WTH3-3.1-1 | MUST NOT | A client does not send a WebTransport CONNECT until the server's SETTINGS carry `SETTINGS_WT_ENABLED` = 1. | WebTransportSettingsTests.Session_ServerLeavesOutWebTransportEnabled_SendsNoConnectAndExits8 |
| WebTransport:WTH3-3.1-2 | MUST | A client that supports WebTransport sends `SETTINGS_WT_ENABLED` = 1. | WebTransportSettingsTests.Session_ClientSettings_CarryWebTransportEnabledAndH3Datagram |
| WebTransport:WTH3-3.1-3 | MUST | Both endpoints send `SETTINGS_H3_DATAGRAM` = 1, since a session relies on HTTP datagrams. | WebTransportSettingsTests.Session_ClientSettings_CarryWebTransportEnabledAndH3Datagram |
| WebTransport:WTH3-3.1-4 | MUST | Both endpoints send the `max_datagram_frame_size` transport parameter with a value above 0. | n/a: the reference server records no QUIC transport parameters; the client's value is pinned by `WebTransportQuicDialer`'s unit tests |
| WebTransport:WTH3-3.2-1 | MUST | The session is opened by an extended CONNECT whose `:protocol` is `webtransport-h3`, whose `:scheme` is `https`, and which carries `:authority` and `:path`. | WebTransportConnectTests.Connect_FieldSection_IsAnExtendedConnectForWebTransportH3OverHttps |
| WebTransport:WTH3-3.3-1 | MUST | A client treats any response status outside 2xx as a failure to establish the session. | WebTransportConnectTests.Connect_Status404_FailsTheSessionWithExit22AndOpensNoStream |
| WebTransport:WTH3-4-1 | MUST NOT | A client opens no WebTransport stream and sends no datagram for a session the server has not accepted. | WebTransportConnectTests.Connect_Status404_FailsTheSessionWithExit22AndOpensNoStream |
| WebTransport:WTH3-4.2-1 | MUST | A WebTransport unidirectional stream starts with stream type `0x54` followed by the session ID. | WebTransportExchangeTests.Unidirectional_Data_GoesAfterType0x54AndTheSessionIdAndTheServerStreamIsWritten |
| WebTransport:WTH3-4.3-1 | MUST | A WebTransport bidirectional stream starts with the signal value `0x41` followed by the session ID. | WebTransportExchangeTests.Bidirectional_Data_GoesAfterSignal0x41AndTheSessionIdAndTheEchoIsWritten |
| WebTransport:WTH3-4.6-1 | MUST | A session's datagram is an HTTP datagram whose quarter stream ID is the CONNECT stream's ID divided by four. | WebTransportExchangeTests.Datagram_Data_CarriesTheQuarterStreamIdOfTheSessionAndTheEchoIsWritten |
| WebTransport:WTH3-6-1 | MUST | An endpoint that ends a session sends `WT_CLOSE_SESSION` (a 32-bit error code and a message of at most 1024 bytes) on the CONNECT stream and then its FIN. | WebTransportCloseTests.ClientClose_AfterTheExchange_SendsWtCloseSessionCode0ThenFin |
| WebTransport:WTH3-6-2 | MUST | On `WT_CLOSE_SESSION` from the peer, an endpoint ends the session's streams, sends no further capsule and closes its side of the CONNECT stream with a FIN. | WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin |
| WebTransport:WTH3-6-3 | MUST | A session's CONNECT stream carries only capsules after the response. | WebTransportCloseTests.ClientClose_AfterTheExchange_SendsWtCloseSessionCode0ThenFin |
| WebTransport:RFC9220-3-1 | MUST NOT | A client does not send an extended CONNECT until the server's SETTINGS carry `SETTINGS_ENABLE_CONNECT_PROTOCOL` = 1. | WebTransportSettingsTests.Session_ServerLeavesOutEnableConnectProtocol_SendsNoConnectAndExits8 |
| WebTransport:RFC9220-3-2 | MUST | An extended CONNECT carries the `:protocol` pseudo-header and, unlike a plain CONNECT, `:scheme` and `:path`. | WebTransportConnectTests.Connect_FieldSection_IsAnExtendedConnectForWebTransportH3OverHttps |
| WebTransport:RFC9297-2.1-1 | MUST | An HTTP/3 datagram's payload starts with the quarter stream ID of the request stream it belongs to. | WebTransportExchangeTests.Datagram_Data_CarriesTheQuarterStreamIdOfTheSessionAndTheEchoIsWritten |
| WebTransport:RFC9297-2.1.1-1 | MUST NOT | An endpoint sends no HTTP/3 datagram until the peer's SETTINGS carry `SETTINGS_H3_DATAGRAM` = 1. | WebTransportSettingsTests.Session_ServerLeavesOutH3Datagram_SendsNoConnectAndExits8 |
| WebTransport:RFC9297-3.2-1 | MUST | A capsule is a type, a length and a value, sent in DATA frames on the request stream. | WebTransportCloseTests.ClientClose_AfterTheExchange_SendsWtCloseSessionCode0ThenFin |
| WebTransport:RFC9221-3-1 | MUST NOT | An endpoint sends no DATAGRAM frame larger than the peer's `max_datagram_frame_size`. | n/a: the reference server's transport accepts every frame size the client could send in one `-d`; the client's size check is pinned by its unit tests |

# Curl.Protocol.Ws.UnitLibrary

Phase 4.

WebSocket, reached by an HTTP upgrade handshake then frame exchange.

**URL schemes:** `ws`, `wss`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

Diagnostic log (`--log-level`, ADR-0222, BL-929): `WsTransferLog` writes under the `ws`
component - the upgrade request by method and request target and the upgrade accepted
(`info`), each frame received and the upload frame sent by opcode, FIN and length
(`verbose`), a close frame whose code is neither 1000 nor 1001 (`warning`) - and, for every
transfer, its end: bytes and milliseconds at `info`, or its `CurlExitCode` at `error`. The
URL's credentials and the `Authorization` value are never written.

`--trace-config ws` (ADR-0370, BL-1164): `WsFrameTrace` writes curl 8.21.0's `[WS]` lines
through the transfer's events when the handler's `TracesFrames` is set - the chunk size and
upload reader after the `101`, each frame decoded and each run of payload passed on (from
`WsFrameDecoder`), the pong sent (from `WsFrameReceiver`), the upload frame encoded and
`websocket established, callback mode`.

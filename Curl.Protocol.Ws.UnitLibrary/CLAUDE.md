# Curl.Protocol.Ws.UnitLibrary

Phase 4.

WebSocket, reached by an HTTP upgrade handshake then frame exchange.

**URL schemes:** `ws`, `wss`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

**Seam:** `IConnector` (ADR-0005). `WsProtocolHandler` takes an `IConnector` in its
constructor and asks it for one connection per transfer; it never takes an
`IConnection` there.

libcurl's WebSocket API (ADR-0607, BL-2471): `WsProtocolHandler.ConnectAndUpgradeOnlyAsync`
runs the same upgrade as `ExecuteAsync` but stops after the `101`, returning a
`WsUpgradeOutcome` with the open connection (the caller owns it) and the bytes after the head;
`WsFrameEncoder.Encode(opcode, isFinal, ...)` writes any masked client frame;
`WsFrameMetadataDecoder` decodes incrementally into `WsFrameRun`s (opcode, FIN, offset, bytes
left) with the completed ping and the tool decoder's own violation messages. These types are the
library's own (`WsOpcode` included): it must not reference `Curl.Api`.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnector`
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

`CURLWS_RAW_MODE` on a transfer that exchanges frames (BL-2494): with `WsProtocolHandler.RawMode`
set, `WsFrameReceiver` hands every byte after the `101` to the output as it came, frame heads
included, decoding nothing and answering no ping, as curl 8.22.0's `ws_cw_write` passes the body
on under `ws_raw_mode`; the `-T` upload goes out unframed too (BL-2526), as `Curl_ws_accept` adds the frame-encoding
client reader only when `ws_raw_mode` is off.

# Curl.Original.Protocol.Ws.UnitLibrary

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
left) with the completed ping and the tool decoder's own violation messages. These types live in the shared
`Curl.WebSocket.UnitLibrary` (ADR-1017), `WsOpcode` included: this library must not reference `Curl.Api`.

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
`WsFrameDecoder` through `WsFrameDecodeReporter`), the pong sent (from `WsFrameReceiver`), the upload frame encoded and
`websocket established, callback mode`.

`CURLWS_RAW_MODE` on a transfer that exchanges frames (BL-2494): with `WsProtocolHandler.RawMode`
set, `WsFrameReceiver` hands every byte after the `101` to the output as it came, frame heads
included, decoding nothing and answering no ping, as curl 8.22.0's `ws_cw_write` passes the body
on under `ws_raw_mode`; the `-T` upload goes out unframed too (BL-2526), as `Curl_ws_accept` adds the frame-encoding
client reader only when `ws_raw_mode` is off.

A read callback's upload (BL-2611, ADR-0669): an upload stream that implements `IWsUploadFrameSource` with non-null
`WsUploadFrames` goes out read by read, as curl 8.22.0's `cr_ws_read`: each read of up to 64 KiB
(`UploadReadSize`) is one binary frame of its length, unless `WsUploadFrames.StartFrame` (libcurl's
`curl_ws_start_frame`, called from the callback) started a frame, whose payload the following reads fill, each asked
for no more than its payload left. Any other upload (`-T`, BL-2683) goes out the same way, but each read is
filled to 64 KiB first, as the tool's `fread` fills it: a 200 000-byte file is frames of 65 536, 65 536, 65 536
and 3 392 bytes, and an empty one sends no frame and writes `We are completely uploaded and fine`.

`curl_ws_send` from a write callback (BL-2710): with `WsProtocolHandler.PerformConnection` set, a transfer that exchanges
frames shows its connection on that `WsPerformConnection` from the `101` until it ends, so the engine's
`ManagedEasyWebSocket` can write to it from inside the write callback of a perform that is not connect-only, as curl
8.22.0's `curl_ws_send` writes to `data->conn` (upstream test2301).

`curl_ws_meta` from a write callback (BL-2711): with `PerformConnection` set and `RawMode` clear, `WsFrameReceiver` also
decodes each read with `WsFrameMetadataDecoder` and writes the payload one run at a time (pings' runs and empty runs
left out, so the bytes written are the same), keeping each run on `WsPerformConnection.Frame` before its write, and the
data message's opcode on `MessageOpcode`, so the engine's `CurrentFrame` answers that write's frame (upstream test2302).
Without `PerformConnection` (the curl tool) each read's payload is still one write.

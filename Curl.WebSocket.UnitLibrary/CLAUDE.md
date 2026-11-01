# Curl.WebSocket.UnitLibrary

The RFC 6455 section 5 frame codec, shared by `Curl.Original.Protocol.Ws.UnitLibrary`, the engine and
extensions (ADR-0999 lets an extension reference a shared layer, never a protocol library):
`WsOpcode`, `WsFrameEncoder` (masked client frames from an `IWebSocketRandomSource`),
`WsFrameDecoder` (server frames read incrementally into `WsDecodedBytes`, each violation as curl
8.21.0's `[WS]` message), `WsFrameMetadataDecoder` (the same decoding as `WsFrameRun`s for
libcurl's `curl_ws_recv` and `curl_ws_meta`).

This library references no other project, and above all no `Curl.Protocol.*` library: the decoder
tells an `IWsFrameDecodeObserver` what it decodes, and the protocol turns that into its diagnostic
log and `--trace-config ws` lines, and a violation into exit 56.

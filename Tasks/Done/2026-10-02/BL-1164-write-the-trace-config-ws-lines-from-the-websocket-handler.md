---
id: BL-1164
title: Write the --trace-config ws lines from the WebSocket handler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1164 — Write the --trace-config ws lines from the WebSocket handler

## Goal

Under `-v --trace-config ws` (and `protocol`, `all`) Curl writes curl 8.21.0's `* [WS] ...` lines from its WebSocket handler, at the same steps and with the same text.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` (`CurlComposition`) decides whether the component is on (`ws`, `protocol` or `all` in `CommandLineOptions.TraceComponents`) and hands the WebSocket handler an `ITransferEvents` sink; the Ws library writes each line at its own step.
- Measured lines are in Notes.

## Acceptance criteria

- [x] The measured exchange in Notes under `-v --trace-config ws` writes the measured `[WS]` lines, in that order, among the `-v` lines; a test pins them.
- [x] Each decoded frame writes its `decoded ... [<TYPE> payload=<n>/<len>]` lines for its own type and length (tests for TEXT, BINARY, CLOSE at least).
- [x] `protocol` and `all` write the same lines; `-v` alone, another component, and `ws` without `-v` write none (tests).
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02 (BL-1104), curl 8.21.0 Schannel,
`Record-CurlExchange.ps1 -Response 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n\x81\x02hi\x88\x00' -CurlArgs '-sS','--trace-config','ws','-v','ws://127.0.0.1:P/'` (exit 0), stderr after the response head:

```
* Received 101, Switching to WebSocket
* [WS] WS, using chunk size 65535
* [WS] Received 101, switch to WebSocket
{ [6 bytes data]
* [WS] decoded decoded [TEXT payload=0/2]
* [WS] passed 2 bytes payload, 0 remain
* [WS] decoded passing [TEXT payload=2/2]
* [WS] decoded decoded [CLOSE payload=0/0]
* [WS] websocket established, callback mode
{ [0 bytes data]
* shutting down connection #0
```

Delivered 2026-10-02 (ADR-0370). `WsFrameTrace` writes the lines; `WsProtocolHandler.TracesFrames` turns it on; `CurlComposition.TracesWs` sets it from `ws`, `protocol` or `all`.

Further measured the same day, same recorder, frames after the 101 head in one read (stderr from `Received 101` on):
- `\x82\x03abc\x88\x02\x03\xe8`: `decoded decoded [BIN payload=0/3]`, passed 3/0 remain, `decoded passing [BIN payload=3/3]`, then the same three for `[CLOSE payload=0/2]`..`2/2`, established.
- `\x01\x01a\x80\x01b`: `[TEXT NON-FINAL payload=0/1]` and `[CONT payload=0/1]` frames.
- `\x89\x01p\x89\x02qq` (held open): `auto PONG to [PING payload=0/1]` right after each ping's `decoded decoded`; after the read's frames, one pong for the last: `WS-ENC: sending [PONG payload=0/2]`, `WS-ENC: buffered [PONG payload=2/2]`, `flushed 8 bytes`; then established.
- `\x89\x00\x81\x01z`: empty ping writes only `decoded decoded` and `auto PONG`; pong lines `payload=0/0` twice, `flushed 6 bytes`.
- Nothing after the head: established straight after the switch line, then `{ [0 bytes data]`, Empty reply.
- `-T` 5-byte file (held open): `UPLOAD set, add ws-encode reader` after the switch line; after established, `WS-ENC: sending [BIN payload=0/5]`, `WS-ENC: buffered [BIN payload=5/5]`, `} [11 bytes data]`. Without holding open, curl writes `abort upload` instead and sends nothing; not modelled (ADR-0370).
- `-I`: `{ [4 bytes data]` then established, no decode lines.

Choices: a frame whose head ends a read writes `decoded passing [... payload=0/<len>]` at once, from curl's `ws_dec_pass` fall-through (unmeasurable with the one-write recorder). The runner test pins `ws` and `protocol` byte for byte; `all` also adds times and ids and other components, so the composition test covers it.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ws (protocol, all) writes curl 8.21.0's [WS] frame decode, pong, upload and established lines

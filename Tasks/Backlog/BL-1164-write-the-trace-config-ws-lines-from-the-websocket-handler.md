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
completed:
---
# BL-1164 — Write the --trace-config ws lines from the WebSocket handler

## Goal

Under `-v --trace-config ws` (and `protocol`, `all`) Curl writes curl 8.21.0's `* [WS] ...` lines from its WebSocket handler, at the same steps and with the same text.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` (`CurlComposition`) decides whether the component is on (`ws`, `protocol` or `all` in `CommandLineOptions.TraceComponents`) and hands the WebSocket handler an `ITransferEvents` sink; the Ws library writes each line at its own step.
- Measured lines are in Notes.

## Acceptance criteria

- [ ] The measured exchange in Notes under `-v --trace-config ws` writes the measured `[WS]` lines, in that order, among the `-v` lines; a test pins them.
- [ ] Each decoded frame writes its `decoded ... [<TYPE> payload=<n>/<len>]` lines for its own type and length (tests for TEXT, BINARY, CLOSE at least).
- [ ] `protocol` and `all` write the same lines; `-v` alone, another component, and `ws` without `-v` write none (tests).
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

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

## Log

- 2026-10-02: Created.

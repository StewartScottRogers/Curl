---
id: BL-4221
title: WebSocketRepl extension: projects, RFC 6455 opening handshake and interactive session over IConnection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketRepl.UnitLibrary, Curl.Extension.WebSocketRepl.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4221 — WebSocketRepl extension: projects, RFC 6455 opening handshake and interactive session over IConnection

## Goal

`Curl.Extension.WebSocketRepl.UnitLibrary` holds the RFC 6455 client opening handshake and an interactive session over an `IConnection`: typed lines become text or binary messages, replies are shown, pings are answered with pongs, and the session closes cleanly.

## Context

Split from BL-4111 (`--ws-repl`, extension 10 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: RFC 6455. Reference `Curl.Protocol.Abstractions.UnitLibrary` (for `IConnection`) and the shared frame codec `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `WsOpcode`, `IWebSocketRandomSource`); never `Curl.Protocol.Ws.UnitLibrary` or another extension. Use the `new-project` skill; the slnx line is one single-line `<Folder Name="/Extensions/WebSocketRepl/">` directly under the marker comment (ADR-1003). The session takes its input as a `Stream` or `TextReader` and its output as a `Stream`, so the hook BL-4111 adds can hand it standard input and the transfer's output. Do not register the extension in any catalogue here: BL-4111 does that.

## Acceptance criteria

- [ ] `Curl.Extension.WebSocketRepl.UnitLibrary` and `Curl.Extension.WebSocketRepl.UnitTests` exist in `/Extensions/WebSocketRepl/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] The opening handshake request (RFC 6455 section 4.1: GET, Host, Upgrade, Connection, Sec-WebSocket-Key from 16 random bytes, Sec-WebSocket-Version 13) is written and the server's response is validated (section 4.1 client requirements and 4.2.2 Sec-WebSocket-Accept); unit tests with recorded bytes, including the RFC's own key/accept example.
- [ ] The session sends each typed line as a masked text frame, and a line command (named in this task's Notes) sends binary, ping and close; received text, binary, ping (answered by pong, section 5.5.2/5.5.3) and close (answered and ended, section 5.5.1/7) frames are handled; unit tests with a fake `IConnection`.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4111's lane 2 prerequisite pass. BL-4111 then adds the transfer hook that takes over a `ws://`/`wss://` transfer when `--ws-repl` is given (through BL-4211's connector seam), the option, description and hook catalogue lines, the ADR, the `CURL_EXTENSIONS=0` test and its quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7ea3c216 6019acf2 ddd0c956 3180db99 3d836d5e.

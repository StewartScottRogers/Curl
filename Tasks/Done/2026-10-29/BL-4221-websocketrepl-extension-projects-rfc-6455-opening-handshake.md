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
completed: 2026-10-29
---
# BL-4221 — WebSocketRepl extension: projects, RFC 6455 opening handshake and interactive session over IConnection

## Goal

`Curl.Extension.WebSocketRepl.UnitLibrary` holds the RFC 6455 client opening handshake and an interactive session over an `IConnection`: typed lines become text or binary messages, replies are shown, pings are answered with pongs, and the session closes cleanly.

## Context

Split from BL-4111 (`--ws-repl`, extension 10 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: RFC 6455. Reference `Curl.Protocol.Abstractions.UnitLibrary` (for `IConnection`) and the shared frame codec `Curl.WebSocket.UnitLibrary` (`WsFrameEncoder`, `WsFrameDecoder`, `WsOpcode`, `IWebSocketRandomSource`); never `Curl.Protocol.Ws.UnitLibrary` or another extension. Use the `new-project` skill; the slnx line is one single-line `<Folder Name="/Extensions/WebSocketRepl/">` directly under the marker comment (ADR-1003). The session takes its input as a `Stream` or `TextReader` and its output as a `Stream`, so the hook BL-4111 adds can hand it standard input and the transfer's output. Do not register the extension in any catalogue here: BL-4111 does that.

## Acceptance criteria

- [x] `Curl.Extension.WebSocketRepl.UnitLibrary` and `Curl.Extension.WebSocketRepl.UnitTests` exist in `/Extensions/WebSocketRepl/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [x] The opening handshake request (RFC 6455 section 4.1: GET, Host, Upgrade, Connection, Sec-WebSocket-Key from 16 random bytes, Sec-WebSocket-Version 13) is written and the server's response is validated (section 4.1 client requirements and 4.2.2 Sec-WebSocket-Accept); unit tests with recorded bytes, including the RFC's own key/accept example.
- [x] The session sends each typed line as a masked text frame, and a line command (named in this task's Notes) sends binary, ping and close; received text, binary, ping (answered by pong, section 5.5.2/5.5.3) and close (answered and ended, section 5.5.1/7) frames are handled; unit tests with a fake `IConnection`.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4111's lane 2 prerequisite pass. BL-4111 then adds the transfer hook that takes over a `ws://`/`wss://` transfer when `--ws-repl` is given (through BL-4211's connector seam), the option, description and hook catalogue lines, the ADR, the `CURL_EXTENSIONS=0` test and its quality run.
- Line commands (ADR-1037): `/binary <text>` sends the text's UTF-8 bytes as binary, `/ping [text]` a ping (cut to
  125 bytes), `/close` a close with code 1000 and stops reading lines, `//...` sends the line without its first `/`
  as text; every other line is sent as text. End of input also sends close 1000. Received text is shown as it is,
  binary as lowercase hex, one line per message; control frames are not shown.
- Entry points for BL-4111: `WebSocketOpeningHandshake.PerformAsync(IConnection, authority, pathAndQuery, ct)`
  returns `WebSocketHandshakeResult(Failure, AlreadyReceived)`; `WebSocketReplSession.RunAsync(TextReader, Stream,
  alreadyReceived, ct)` returns `WebSocketReplOutcome(Ending, ViolationMessage)`. Failures are returned, not thrown,
  so the hook picks the exit code.
- References `Curl.Protocol.Abstractions` and `Curl.WebSocket` (the shared codec) directly; the build's
  `VerifyExtensionIsolation` passes. Measured: 100% line, 100% branch, worst CRAP 10, 0 failing members (35).
  Full build clean and all fast tests green on Windows; the tests use only in-memory fakes, so they are platform-neutral.
- Delivered the `feature` pipeline's stages directly, without sub-agents, to stay inside the run's cost cap.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7ea3c216 6019acf2 ddd0c956 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The WebSocketRepl extension library performs the RFC 6455 opening handshake and runs an interactive session over IConnection

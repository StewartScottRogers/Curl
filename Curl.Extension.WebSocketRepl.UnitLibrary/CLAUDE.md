# Curl.Extension.WebSocketRepl.UnitLibrary

The interactive WebSocket session extension behind `--ws-repl` (ADR-0999, ADR-1037). Namespace
`Curl.Extension.WebSocketRepl`. Tested against RFC 6455, not against curl.

- `Handshake/`: the client's opening handshake. `WebSocketHandshakeKey` makes the `Sec-WebSocket-Key` and its
  `Sec-WebSocket-Accept`; `WebSocketHandshakeRequest` writes the section 4.1 request;
  `WebSocketHandshakeResponseValidator` checks the response; `WebSocketOpeningHandshake` runs both over an
  `IConnection` and hands back the frames that came with the response (`WebSocketHandshakeResult`).
- `Session/`: `WebSocketReplSession` sends each typed line as a masked frame (`WebSocketReplLine`: `/binary`,
  `/ping`, `/close`, `//` to send a line starting with `/`), shows received messages (`WebSocketReplOutput`:
  text as it is, binary as hex, one line each), answers pings and closes, and says why it ended
  (`WebSocketReplOutcome`).

References only `Curl.Protocol.Abstractions` and the shared frame codec `Curl.WebSocket`. No option, hook or
registration yet: BL-4111 adds them. Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.WebSocketRepl.UnitTests`.

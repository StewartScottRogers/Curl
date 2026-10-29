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
- `Transfer/`: `WebSocketReplTransferHook` takes over a `ws://` or `wss://` transfer when `--ws-repl` is given
  (ADR-1081): it connects through curl's connector, performs the handshake and runs the session on standard input
  and the transfer's output. Any other scheme's transfer goes on unchanged.
- `WebSocketReplExtension`: the `--ws-repl` option, the description and the hook activator that
  `Curl.Extension.Composition` registers.

References only `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions` and the shared frame codec
`Curl.WebSocket`. Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.WebSocketRepl.UnitTests`.

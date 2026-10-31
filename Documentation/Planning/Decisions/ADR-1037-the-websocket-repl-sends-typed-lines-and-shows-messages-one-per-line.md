# ADR-1037 — The WebSocket REPL sends typed lines and shows messages one per line

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4221.

## Context

BL-4111 adds `--ws-repl`, an interactive WebSocket session, as the extension
`Curl.Extension.WebSocketRepl.UnitLibrary` (ADR-0999). Upstream curl has no such mode, so RFC 6455 decides the
wire behaviour; how lines become frames and how frames are shown is ours to choose. BL-4221 is the library part:
the opening handshake and the session over an `IConnection`, with no option or hook.

## Decision

- **Handshake.** The client sends RFC 6455 section 4.1's request with no subprotocol or extension, and fails the
  connection on any response other than `101` with `Upgrade: websocket`, a `Connection` holding `Upgrade`, one
  `Sec-WebSocket-Accept` equal to section 4.2.2's value for the key sent, and no `Sec-WebSocket-Extensions` or
  `Sec-WebSocket-Protocol`. A response head over 65536 bytes, or a connection that ends inside it, fails too.
  Failures are returned as text, not thrown, so the hook BL-4111 adds chooses its exit code.
- **Lines.** Each line is sent as one masked text frame. `/binary <text>` sends the text's UTF-8 bytes as a
  binary frame; `/ping [text]` a ping (cut to 125 bytes); `/close` a close with code 1000, after which no more
  lines are read; a line starting with `//` is sent as text without its first `/`. Any other line, including one
  starting with an unknown `/` command, is sent as it is: nothing is swallowed.
- **Shown.** A text message's bytes are written as they arrive, a binary message's as lowercase hexadecimal,
  each message ended by a line feed. Pings, pongs and closes are not shown.
- **Closing.** The end of input sends close 1000, as `/close` does, and the session reads on until the server's
  close or the end of the connection. A close from the server first is answered with its status code (or with
  none when it carried none) and ends the session (RFC 6455 section 5.5.1). Only one close is ever sent; pongs
  may still follow it. A frame the shared decoder rejects sends close 1002 and ends the session with the
  decoder's `[WS]` message.
- **Concurrency.** Lines are read and frames received at the same time; one lock orders the frames written.

## Consequences

The session takes a `TextReader` and a `Stream`, so BL-4111's hook hands it standard input and the transfer's
output. A server that never answers the client's close keeps the session reading; the hook's timeouts bound it.

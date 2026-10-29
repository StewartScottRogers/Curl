# ADR-1076 — --ws-repl is a transfer hook that takes over ws and wss transfers

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4111.

## Context

BL-4111 gives Curl an interactive WebSocket session, `--ws-repl`, as the extension
`Curl.Extension.WebSocketRepl.UnitLibrary` (ADR-0999). BL-4221 built its opening handshake and session
(ADR-1037); BL-4211 gave option-activated transfer hooks curl's connector (ADR-1039). Upstream curl has no such
option, so RFC 6455 (December 2011, the version every WebSocket server speaks, `Sec-WebSocket-Version: 13`) is the
specification followed. `ws://` and `wss://` are curl's own schemes, so the extension may not register a protocol
handler for them: that would change what curl does without the option.

## Decision

- **Name.** One flag, `--ws-repl`, no argument. The extension's description is named `ws-repl` and cites
  RFC 6455; it claims no scheme.
- **Shape.** `WebSocketReplExtension.HookFor` activates `WebSocketReplTransferHook` only when `--ws-repl` is given,
  registered by one line each in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and
  `ExtensionTransferHookCatalogue`. `ExtensionCatalogue.cs` holds protocol handler factories, so it gets no line.
  Without the option no hook runs and every transfer is unchanged; with `CURL_EXTENSIONS=0` the option is not
  registered, so curl's parser refuses it as an unknown option (exit 2).
- **Takeover.** On a `ws` or `wss` URL (scheme compared case-insensitively) the hook never calls the next step, so
  curl's own WebSocket handler never runs. It connects through the option group's connector to the URL's host and
  port, with TLS for `wss` and through the transfer's proxy, performs section 4.1's handshake (`Host` carries the
  port only when it is not the default; the request target is the path as curl sends it plus the query), and runs
  the session with standard input as the typed lines and the transfer's output (stdout or `-o`) as where messages
  are shown. Any other scheme's transfer goes on unchanged: the option does nothing there.
- **Exit codes.** A failed connect keeps the connector's exit code and message (`Could not connect to <host>: ...`);
  a refused handshake is `CURLE_WEIRD_SERVER_REPLY` (8, `Refused WebSocket upgrade: <why>`); a server that breaks
  the framing rules is `CURLE_RECV_ERROR` (56, `WebSocket protocol violation: <why>`), as curl reports a receive
  error; a session that closes, either side first, or whose connection ends, succeeds. A host that gives the hook
  no connector fails it with `CURLE_FAILED_INIT` (2).

## Left out

- Subprotocols (`Sec-WebSocket-Protocol`) and extensions such as permessage-deflate (RFC 7692): no option asks for
  them, and ADR-1037's handshake refuses a server that picks one.
- curl's own request options (`-H`, `-u`, cookies) are not added to the handshake: the session is the extension's,
  and the handshake ADR-1037 pins sends only RFC 6455's headers. A later task can add them if wanted.
- `--write-out` and progress see a transfer of 0 bytes: the session's messages are not counted.

## Consequences

- `curl --ws-repl wss://echo.example/` holds a session from the console; `curl --extensions` and
  `curl --ai-help extensions` list it.
- Tested against RFC 6455 with scripted connections and a recording connector, never a network.

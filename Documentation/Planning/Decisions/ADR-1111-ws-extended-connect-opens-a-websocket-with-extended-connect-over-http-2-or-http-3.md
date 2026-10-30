# ADR-1111 — --ws-extended-connect opens a WebSocket with extended CONNECT over HTTP/2 or HTTP/3

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4297.

## Context

BL-4297 ties `Curl.Extension.WebSocketExtendedConnect.UnitLibrary`'s HTTP/2 flow (BL-4295) and HTTP/3 flow
(BL-4296) on its wire layer (BL-4298) to the command line. Upstream curl opens ws:// and wss:// only with the
HTTP/1.1 upgrade, so there is no curl output to match: RFC 8441 (Bootstrapping WebSockets with HTTP/2) and RFC 9220
(Bootstrapping WebSockets with HTTP/3) decide the behaviour, and the extension rules of ADR-0999 apply.

## Decision

- **One option, `--ws-extended-connect <h2|h3>`.** One name with the HTTP version as its argument, rather than two
  flags, so the choice cannot be given both ways. Any other argument fails the transfer with exit 2 (FailedInit)
  and a message naming the two values.
- **A transfer hook, registered like `--ws-subprotocol`.** The extension adds no URL scheme, so it registers its
  option, description and transfer hook in the option, description and transfer hook catalogues of
  `Curl.Extension.Composition` (ADR-1006, ADR-1007), one line each, not a protocol handler factory in
  `ExtensionCatalogue.cs`. The hook acts only on ws and wss URLs and only when the option was given, so without the
  option every transfer is unchanged, and with `CURL_EXTENSIONS=0` the option is not registered and the command
  line refuses it as curl refuses an unknown option (exit 2).
- **Connections come from the host's connector (ADR-1039).** `h2` asks for a TCP connection with ALPN `h2` (TLS for
  wss, prior-knowledge cleartext HTTP/2 for ws) and fails with exit 16 (HTTP/2) when a TLS server negotiates
  anything else. `h3` asks for a QUIC connection with ALPN `h3`; ws:// with `h3` fails with exit 1
  (UnsupportedProtocol) because HTTP/3 always runs over TLS. A connector failure keeps its own exit code and message.
- **The session.** Any `-d` data is sent as one text message; every text, binary and continuation payload received
  is written to the output; a ping is answered with a pong carrying its payload; the server's close is echoed and the
  stream ended; the transfer ends there or when the server ends its stream. An extended CONNECT the server refuses
  (no SETTINGS_ENABLE_CONNECT_PROTOCOL, a non-2xx answer, a reset) fails with exit 16 over HTTP/2 and exit 95
  (HTTP/3) over HTTP/3, carrying the flow's message.

## Left out

- **Subprotocol negotiation.** No `Sec-WebSocket-Protocol` is offered; `--ws-subprotocol` keeps to the HTTP/1.1
  upgrade. Combining them needs the subprotocol extension to hand its opening messages to this one, which neither
  task asked for.
- **Proxies, connection reuse and falling back to HTTP/1.1.** The WebSocket gets a connection of its own and fails
  rather than downgrading, since the user asked for extended CONNECT by name.
- **Sending from standard input.** Only `-d` data is sent; reading messages from an upload stream stays with the
  HTTP/1.1 path, since the flows give no way to interleave sending with receiving.

## Consequences

`curl --extensions` and `curl --ai-help extensions` list `ws-extended-connect` from its description. The flows of
ADR-1098 and BL-4295 are reachable from the command line.

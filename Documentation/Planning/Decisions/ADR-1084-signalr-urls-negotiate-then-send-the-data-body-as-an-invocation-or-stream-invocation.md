# ADR-1084 — SignalR URLs negotiate, then send the data body as an invocation or stream invocation

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4108.

## Context

BL-4108 finishes the SignalR extension (ADR-0999) `Curl.Extension.Protocol.SignalR.UnitLibrary`, whose JSON hub
protocol codec (ADR-1032) and negotiate and WebSocket transport (ADR-1038) are already built. Upstream curl
has no SignalR support, so the specifications decide: ASP.NET Core's `HubProtocol.md` and
`TransportProtocols.md` (dotnet/aspnetcore `src/SignalR/docs/specs`, as published in 2026): the JSON hub
protocol version 1 and negotiate version 1 (version 0 responses are read too). A protocol handler sees no
extension option values (`ITransferContext` carries none), so the call has to come from the URL or the body.

## Decision

- Schemes `signalr://host[:port]/hub[?query]` and `signalrs://`. The hub endpoint is the same host, port, path
  and query over `http` or `https` (TLS through curl's connector); default ports 80 and 443, as the reference
  client's URLs. No extension option: the extension adds schemes only, as Socket.IO does (ADR-1035).
- The `-d` body is the call, written as the hub message itself without its id:
  `{"type":1,"target":"Method","arguments":[...]}` sends an Invocation, `"type":4` a StreamInvocation; `type`
  defaults to 1 and `arguments` to `[]`. The client gives it the invocation id `1` so the hub always answers.
  The Completion's result is written as one JSON line (nothing when it has none); each StreamItem as one JSON
  line until the Completion. Using the spec's own message shape needs no new syntax to learn.
- Without `-d` the client only listens: each Invocation the hub makes is written as the JSON array
  `["target", args...]` per line (the shape Socket.IO events use, ADR-1035) until a Close message or the
  WebSocket's close.
- Exit codes: a `-d` body that is not such a message is 2 (as Socket.IO's bad `-d`), before connecting; a
  handshake error, a Completion error or a Close with an error is 22 (the hub refused, as Socket.IO's namespace
  refusal); a failed negotiate, a refused upgrade or a record that breaks the protocol is 8, "weird server
  reply", with the message prefixed `SignalR: `; a connect failure is the connector's own result.

## Left out, and why

- Client-sent Pings (HubProtocol.md, "Ping") and the server timeout: the handler has no timer seam, and a
  curl transfer is short-lived; a long listen relies on the server's own keep-alive Pings. `--max-time` still
  ends it.
- The MessagePack hub protocol, and the Server-Sent Events and Long Polling transports: the JSON protocol over
  WebSockets is what every SignalR server offers by default, and curl's output is text.
- Client-to-server streams (`streamIds`) and CancelInvocation: a single `-d` body has no way to feed a stream.
- Stateful reconnect (`useStatefulReconnect`, Ack and Sequence messages): a transfer does not reconnect.

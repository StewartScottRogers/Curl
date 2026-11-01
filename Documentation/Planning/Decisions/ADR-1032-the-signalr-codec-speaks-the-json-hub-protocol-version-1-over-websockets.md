# ADR-1032 — The SignalR codec speaks the JSON hub protocol, version 1, over WebSockets

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4218.

## Context

BL-4108 adds a SignalR client as the extension `Curl.Extended.Protocol.SignalR.UnitLibrary` (ADR-0999). Upstream
curl has no SignalR support, so ASP.NET Core's own specifications decide: `src/SignalR/docs/specs/HubProtocol.md`
and `src/SignalR/docs/specs/TransportProtocols.md` in dotnet/aspnetcore. BL-4218 is the first of its three
parts: the projects and a pure codec of the wire format, with no handler and no registration.

## Decision

- **Version followed.** The JSON hub protocol version 1 (handshake request `{"protocol":"json","version":1}`),
  and the negotiate exchange `POST [endpoint]/negotiate?negotiateVersion=1`, reading both the version 1 and the
  version 0 response, the redirect form (`url`, `accessToken`) and the `error` form, as the specifications on
  dotnet/aspnetcore's main branch describe them in October 2026.
- **Messages.** The client writes the handshake request, Invocation (1), StreamInvocation (4),
  CancelInvocation (5) and Ping (6), each one JSON object ended by 0x1E, and reads the handshake response and
  every message type 1 to 7. A type the specification does not define reads as `UnknownHubMessage`, which the
  client ignores, so a newer server's messages do not end the connection. A Completion carrying both `result`
  and `error` is a format error. `headers` is read past and never written.
- **JSON.** Arguments, items and results are kept as the JSON text the peer sent, read with `Utf8JsonReader` and
  written with `Utf8JsonWriter.WriteRawValue`: no serializer, so nothing needs reflection and the library stays
  AOT-safe; what the text means is the caller's business.
- **Framing.** `HubFrameSplitter` buffers whatever bytes arrive and hands out each record when its 0x1E arrives,
  so a record split at any point reads the same. JSON never holds a raw 0x1E (control characters are escaped),
  so the first 0x1E always ends a record.
- **WebSocket URL.** `http` becomes `ws` and `https` becomes `wss`; the `id` query parameter is the
  `connectionToken` under negotiate version 1 and the `connectionId` under version 0, appended to any query the
  endpoint already has.
- **Left out.** The MessagePack hub protocol: binary records with a variable-length prefix and a MessagePack
  codec, which nothing in the base class library reads; the JSON protocol covers every message, and every
  ASP.NET Core server offers it. The Long Polling and Server-Sent Events transports: every server that offers
  them also offers WebSockets, which Curl already speaks, and they need a second request loop each. A later
  task may add any of them without changing this codec.

## Consequences

- BL-4219 builds the client flow (negotiate, redirect, WebSocket, handshake) on these types in `Transport/`, and
  BL-4108 adds the options and the registration.
- A server that offers only Long Polling or Server-Sent Events, or only MessagePack, is refused by the client.

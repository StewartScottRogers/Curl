# Curl.Extension.SignalR.UnitLibrary

The SignalR client extension (ADR-0999, ADR-1029). Namespace `Curl.Extension.SignalR`. Tested against ASP.NET Core's
`HubProtocol.md` and `TransportProtocols.md` (dotnet/aspnetcore `src/SignalR/docs/specs`), not against curl.

- `HubProtocol/`: the JSON hub protocol. `HubMessageWriter` writes the handshake request, Invocation,
  StreamInvocation, CancelInvocation and Ping, each ended by 0x1E; `HubFrameSplitter` cuts received bytes into
  records; `HubMessageReader` reads the handshake response and the `HubMessage` records of types 1 to 7.
- `Negotiate/`: `NegotiateResponseReader` reads a negotiate response (version 0 or 1, redirect, error) into
  `NegotiateResponse`; `SignalRUrls` builds the negotiate URL and the WebSocket URL with its `id`.
- `Json/`: `JsonFieldReading` and `JsonObjectFields` read JSON with `Utf8JsonReader`, keeping each value's text.
- `Transport/` is BL-4219's client flow. No option, handler or registration yet: BL-4108 adds them.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.SignalR.UnitTests`.

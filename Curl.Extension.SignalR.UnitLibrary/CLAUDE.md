# Curl.Extension.SignalR.UnitLibrary

The SignalR client extension (ADR-0999, ADR-1032). Namespace `Curl.Extension.SignalR`. Tested against ASP.NET Core's
`HubProtocol.md` and `TransportProtocols.md` (dotnet/aspnetcore `src/SignalR/docs/specs`), not against curl.

- `HubProtocol/`: the JSON hub protocol. `HubMessageWriter` writes the handshake request, Invocation,
  StreamInvocation, CancelInvocation and Ping, each ended by 0x1E; `HubFrameSplitter` cuts received bytes into
  records; `HubMessageReader` reads the handshake response and the `HubMessage` records of types 1 to 7.
- `Negotiate/`: `NegotiateResponseReader` reads a negotiate response (version 0 or 1, redirect, error) into
  `NegotiateResponse`; `SignalRUrls` builds the negotiate URL and the WebSocket URL with its `id`.
- `Json/`: `JsonFieldReading` and `JsonObjectFields` read JSON with `Utf8JsonReader`, keeping each value's text.
- `Transport/`: the client flow over `IConnection` (ADR-1038). `SignalRNegotiator` posts the negotiate request,
  follows one redirect and gives a `NegotiatedWebSocketEndpoint`; `SignalRWebSocketUpgrade` sends RFC 6455's client
  handshake and checks the `101`; `SignalRWebSocketSession` sends records as masked text frames and reads them back,
  answering Ping and Close. `HttpRequestWriter`, `HttpResponseReader` and `BufferedConnectionReader` are its own
  HTTP/1.1 (Content-Length, chunked, read to close); every failure is a `SignalRTransportException`.
- `Client/`: the `signalr://` and `signalrs://` handler (ADR-1081). `SignalRProtocolHandler` maps the URL to the
  `http`/`https` endpoint, negotiates, upgrades and runs `SignalRHubSession`, which sends the handshake and the
  `-d` call `SignalRCall` reads (`{"type":1|4,"target":...,"arguments":[...]}`) and writes the result, each stream
  item or each hub call as one JSON line. `SignalRExtension` gives the description and handler factory that
  `Curl.Extension.Composition` registers. No extension option.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and the shared frame
codec `Curl.WebSocket.UnitLibrary`. Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.SignalR.UnitTests`.

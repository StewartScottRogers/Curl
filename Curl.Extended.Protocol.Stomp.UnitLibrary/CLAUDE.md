# Curl.Extended.Protocol.Stomp.UnitLibrary

The STOMP extension (ADR-0999, ADR-1059), tested against STOMP 1.2, not against curl. It references only
`Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and the base class library. The handler
is registered by one line in `ExtensionCatalogue.cs` and described in `ExtensionDescriptionCatalogue.cs`.

`Client/` (namespace `Curl.Extended.Protocol.Stomp.Client`):

- `StompExtension` is the description `curl --extensions-help` lists and the handler factory, over
  `ProtocolHandlerServices.Connector`.
- `StompProtocolHandler` serves `stomp://` (port 61613) and `stomps://` (port 61614, TLS); the URL path is the
  destination, and `--user` gives `CONNECT`'s `login` and `passcode`.
- `StompClientFlow` runs `CONNECT`/`CONNECTED`, then `SEND` and a receipted `DISCONNECT` with a body, or `SUBSCRIBE`
  and each `MESSAGE` body to the output until the server closes. `StompClientMessages` holds the error texts and
  `StompClientException` carries the exit code.

`Wire/` (namespace `Curl.Extended.Protocol.Stomp.Wire`):

- `StompFrame` and `StompHeader` are one frame; `HeaderValue` gives the first of repeated headers.
- `StompFrameEncoding` writes frames and escapes and unescapes header text ("Value Encoding").
- `StompFrameReader` reads frames off an `IConnection`: heart-beat EOLs, CR LF lines, `content-length` bodies.

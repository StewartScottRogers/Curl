# Curl.Extension.Protocol.SocketIo.UnitLibrary

The `socketio://` and `socketios://` extension (ADR-0999, ADR-1035). Namespace `Curl.Extension.Protocol.SocketIo`.
It references only `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions` and the shared frame
codec `Curl.WebSocket.UnitLibrary`, never a `Curl.Protocol.*` library or another extension.

- `SocketIoExtension` holds the description and `CreateHandler`, registered by one line each in
  `Curl.Extension.Composition`'s `ExtensionCatalogue` and `ExtensionDescriptionCatalogue`.
- `SocketIoProtocolHandler` connects through curl's `IConnector`, runs `SocketIoUpgrade` (the RFC 6455
  handshake to `/socket.io/?EIO=4&transport=websocket`) and a `SocketIoSession` (Engine.IO v4 packets,
  Socket.IO v5 `SocketIoPacket`s) over a `WebSocketMessageChannel` (whole text messages, pings answered).
- `SocketIoEmit` reads the `-d` event array.

Tested against the specifications with recorded bytes, never a network, in
`Curl.Extension.Protocol.SocketIo.UnitTests`. Held to the four quality gates in the root `CLAUDE.md`.

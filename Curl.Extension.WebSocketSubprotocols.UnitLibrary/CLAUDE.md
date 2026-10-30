# Curl.Extension.WebSocketSubprotocols.UnitLibrary

The `--ws-subprotocol` extension (ADR-0999, ADR-1085): speaks graphql-transport-ws, WAMP (basic profile, JSON) or
STOMP 1.2 over curl's WebSocket. Namespace `Curl.Extension.WebSocketSubprotocols`. Tested against those
specifications, not against curl.

- `WebSocketSubprotocolsExtension` holds the `--ws-subprotocol` `ExtensionOption`, the `ExtensionDescription` and
  `HookFor`, the transfer hook activator; `Curl.Extension.Composition`'s catalogues register each with one line.
- `WebSocketSubprotocolRequest` reads the value `<name> [argument]` into the subprotocol's name and its opening
  messages, which `GraphQlTransportWsMessages`, `WampMessages` and `StompMessages` write (`SubprotocolJson` writes
  their JSON strings).
- `WebSocketSubprotocolTransferHook` adds `Sec-WebSocket-Protocol` and gives curl's ws handler the messages as a
  `WsMessageUploadStream` (`IWsUploadMessageSource`, one text frame each) through `WebSocketSubprotocolContext`.
  A transfer that is not ws or wss runs unchanged.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and the shared layer
`Curl.WebSocket.UnitLibrary`. Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.WebSocketSubprotocols.UnitTests`.

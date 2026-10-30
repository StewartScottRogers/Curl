# ADR-1082: --ws-subprotocol names a WebSocket subprotocol and sends its opening messages

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4114)

## Context

Extension 13 of 63 (ADR-0999): speak graphql-ws, WAMP and STOMP over curl's WebSocket. Each of the three is a
WebSocket subprotocol: the client asks for it by name in `Sec-WebSocket-Protocol` (RFC 6455 §4.1) and then sends
its messages, one text frame each. curl's ws handler frames a plain upload into binary frames of up to 64 KiB, so
BL-4253 added `IWsUploadMessageSource` to the shared layer `Curl.WebSocket.UnitLibrary`: an upload that gives whole
messages, which the handler sends one final masked frame each.

## Decision

- One option, `--ws-subprotocol "<name> [argument]"`, in `Curl.Extension.WebSocketSubprotocols.UnitLibrary`, a
  transfer hook registered in the option, description and transfer hook catalogues. It adds no URL scheme, so
  `ExtensionCatalogue.cs` (protocol handler factories) gets no line, as with `--jsonrpc` (ADR-1028).
- The name is the subprotocol's registered token, sent as `Sec-WebSocket-Protocol` after curl's own headers:
  - `graphql-transport-ws <operation>`: the graphql-ws library's PROTOCOL.md (the graphql-transport-ws protocol).
    Sends `ConnectionInit` (`{"type":"connection_init"}`) and one `Subscribe` with id `1` whose payload's `query` is
    the operation.
  - `wamp.2.json <realm> [topic]`: the WAMP basic profile (draft-oberstet-hybi-tavendo-wamp, JSON serialization).
    Sends `HELLO` `[1, realm, {"roles":{"subscriber":{}}}]` and, with a topic, `SUBSCRIBE` `[32, 1, {}, topic]`.
  - `v12.stomp [destination]`: STOMP 1.2. Sends `CONNECT` with `accept-version:1.2` and `host:` the URL's host
    and, with a destination, `SUBSCRIBE` with `id:0` and the destination value-encoded. Each frame ends with NUL.
- The messages replace any `-d` or `-T` upload. The handler sends the whole upload once the upgrade is accepted and
  then receives until the server closes, so the messages are pipelined: the hook does not wait for
  `connection_ack`, `WELCOME` or `CONNECTED` before the subscription. All three protocols let a server take them so;
  a server that rejects the session closes it, and its answer is what curl writes.
- What the server sends is written as curl's ws handler writes any message, unchanged.
- Only ws and wss transfers are hooked; any other transfer runs unchanged. A value naming another subprotocol, a
  graphql-transport-ws value without an operation, or a WAMP value without a realm (or with more than a realm and a
  topic) fails the transfer with exit 2 before anything is sent.
- JSON strings are written by `JsonEncodedText` with the relaxed encoder (base class library, AOT-safe), escaping
  only what RFC 8259 §7 requires.

## Left out

- Replying to the server (graphql-transport-ws `Pong`, `Complete`; WAMP `GOODBYE`; STOMP `ACK`, `DISCONNECT`):
  the handler sends only the upload, before it reads; a lasting conversation needs a handler-level seam, not an
  upload. `--max-time` or closing ends the transfer.
- Other message kinds (WAMP `CALL`/`PUBLISH`, STOMP `SEND`, graphql-transport-ws variables and operation names):
  one opening flow per subprotocol keeps the option one value; each can be added as a further argument form.
- The legacy `graphql-ws` subprotocol of subscriptions-transport-ws, WAMP's MessagePack and CBOR serializations
  and STOMP 1.0/1.1 (`v10.stomp`, `v11.stomp`): not named by the task; they would add names to the same switch.
- Checking the server's `Sec-WebSocket-Protocol` answer: curl's handler does not expose it to a hook, and a
  server that did not pick the subprotocol answers the messages with an error or a close.

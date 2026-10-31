# Curl.Extension.Mqtt5.UnitLibrary

The MQTT 5 client extension (ADR-0999), for `mqtt5://`, `mqtt5s://` and `mqtt5+ws://` (BL-4115). Namespace
`Curl.Extension.Mqtt5`. Tested against OASIS MQTT Version 5.0 (7 March 2019), not against curl. Never references
`Curl.Protocol.Mqtt.UnitLibrary` (curl's own MQTT 3.1.1 `mqtt://`) or another extension.

- `Encoding/`: the data types of section 1.5. `Mqtt5Writer` and `Mqtt5Reader` write and read Byte, Two and Four Byte
  Integer, Variable Byte Integer (`Mqtt5VariableByteInteger`), UTF-8 Encoded String (`Mqtt5Utf8String`, with the
  section 1.5.4 rules), Binary Data and UTF-8 String Pair.
- `Packets/`: the control packets of chapter 3. `Mqtt5FixedHeader` frames a packet (section 2.1);
  `Mqtt5PacketReader` reads one whole packet into its `Mqtt5Packet` subclass (CONNECT to AUTH) and each packet
  writes itself with `ToBytes`. `Mqtt5PropertyCodec` reads and writes properties against table 2-4
  (`Mqtt5PropertyDefinitions`); `Mqtt5ReasonCode` is table 2-6 and `Mqtt5ReasonCodes` its success/failure split;
  `Mqtt5Topics` holds the topic name, topic filter and shared subscription rules (sections 4.7 and 4.8.2).
- Every fault in the wire format, read or about to be written, raises `Mqtt5MalformedPacketException` carrying the
  reason code.
- `Client/`: `Mqtt5ProtocolHandler` serves `mqtt5://` and `mqtt5s://` with curl's `mqtt://` behaviour (ADR-1049);
  `Mqtt5ClientFlow` runs CONNECT, then PUBLISH or SUBSCRIBE, over any `IConnection` through `Mqtt5PacketChannel`.
- `WebSocket/`: `Mqtt5WebSocketProtocolHandler` serves `mqtt5+ws://` (ADR-1054): `Mqtt5WebSocketUpgrade` sends the
  RFC 6455 opening handshake to `/mqtt` offering the `mqtt` subprotocol and checks the reply selects it, then
  `Mqtt5WebSocketConnection` carries the same `Mqtt5ClientFlow` in binary frames as an `IConnection` (section 6).
  No option or registration yet: BL-4115 adds them.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and the shared frame codec
`Curl.WebSocket.UnitLibrary`. Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.Mqtt5.UnitTests`.

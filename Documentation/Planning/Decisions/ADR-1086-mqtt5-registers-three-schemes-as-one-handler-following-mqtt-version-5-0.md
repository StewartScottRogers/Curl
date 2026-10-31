# ADR-1086: MQTT 5 registers three schemes as one handler, following OASIS MQTT Version 5.0

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4115)

## Context

BL-4115 adds MQTT 5 as an extension (ADR-0999), tested against OASIS MQTT Version 5.0
(https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html), not against curl, whose `mqtt://` speaks 3.1.1 and must
stay so. BL-4242, BL-4243 and BL-4244 built the wire format (ADR-1044), the client flow (ADR-1049) and the WebSocket
transport (ADR-1054); this task names the schemes, registers them and records what is left out.

## Decision

- **Schemes** `mqtt5` (port 1883), `mqtt5s` (TLS from curl's connector, port 8883) and `mqtt5+ws` (WebSocket to
  `/mqtt`, port 80), the ports the specification's section 4.2 and IANA register for MQTT and secure MQTT and RFC 6455
  gives `ws://`. A `5` suffix keeps them apart from curl's `mqtt://` and `mqtts://`, which the extension never touches:
  with extensions on, `mqtt://` is still curl's 3.1.1 handler. The library is `Curl.Extension.Protocol.Mqtt5.UnitLibrary`; it
  adds no command-line option.
- **One handler, one catalogue line.** `Mqtt5Extension.CreateHandler` returns `Mqtt5SchemeHandler`, which serves all
  three schemes and hands each transfer to a `Mqtt5WebSocketProtocolHandler` for `mqtt5+ws` and to a
  `Mqtt5ProtocolHandler` otherwise, made over the connector for that transfer, so the extension registers with one line in `ExtensionCatalogue.cs` as ADR-0999
  asks.
- **Specification version** 5.0 (OASIS Standard, 7 March 2019): properties (section 2.2.2), reason codes (section
  2.4), shared subscriptions (section 4.8.2, `$share/name/filter` as the URL path) and WebSocket transport (section 6).
- With `CURL_EXTENSIONS=0` the three schemes are unknown, so a URL with one fails as curl fails an unsupported scheme
  (exit 1, `Protocol "mqtt5" not supported`).

## Left out, and why

- **QoS 1 and 2.** Publishing and subscribing stay at QoS 0, as curl's `mqtt://` does: QoS 1 and 2 need a session
  that outlives the transfer to be worth anything, and a one-shot command-line transfer has none.
- **Will messages, AUTH exchanges (enhanced authentication), topic aliases and user properties on the client's own
  packets.** The codec reads and writes them all (ADR-1044), but no curl option maps onto them without inventing new
  options; a later task can add options through the extension option catalogue.
- **A secure WebSocket scheme and another WebSocket path.** `mqtt5+ws` has no TLS twin and always upgrades to
  `/mqtt` (ADR-1054); both would need a new scheme or option, and the common brokers serve `/mqtt` by default.

## Consequences

`curl mqtt5://broker/home/temp -d 21` publishes, `curl mqtt5://broker/'$share/g/home/#'` subscribes as one of a
shared group, `mqtt5s://` and `mqtt5+ws://` do the same over TLS and WebSocket, and all three appear in
`curl --extensions` and `curl --ai-help extensions`.

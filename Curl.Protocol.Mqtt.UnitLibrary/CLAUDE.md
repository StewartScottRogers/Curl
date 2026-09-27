# Curl.Protocol.Mqtt.UnitLibrary

Phase 4.

MQTT 3.1.1 over a byte stream, as curl 8.21.0 speaks it. `MqttProtocolHandler` puts
`ITransferContext.Credentials` in the CONNECT; then, with `PostData` (`-d`) set, it
sends one QoS 0 PUBLISH to the URL's topic and a DISCONNECT, and otherwise it subscribes
to the topic and writes each PUBLISH it receives in curl's format. `MqttSession` runs
one transfer; `MqttPackets` builds every packet sent.

**URL schemes:** `mqtt` (default port 1883), `mqtts` (default port 8883, the same
handler with `ConnectTarget.UseTls` true)

**Seam:** `IConnector` (ADR-0005). The handler asks it for one connection per transfer
and disposes that connection itself. A transfer proxy (`ITransferContext.Proxy`) goes into that
`ConnectTarget`, and the connector tunnels through it (ADR-0056); the handler holds no
proxy code. The client identifier's random eight characters
come from a constructor parameter, so tests fix them.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnector`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

The source of truth for behaviour is curl 8.21.0's `lib/mqtt.c`, plus measurements
against the local curl 8.21.0; the tests name each measured case.

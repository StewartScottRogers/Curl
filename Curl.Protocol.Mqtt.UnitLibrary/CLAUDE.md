# Curl.Protocol.Mqtt.UnitLibrary

Phase 4.

MQTT 3.1.1 over a byte stream: `MqttProtocolHandler` subscribes to the URL's topic and
writes each PUBLISH it receives in curl 8.21.0's format. Publishing with `-d` and
credentials from `-u` are not implemented yet.

**URL schemes:** `mqtt` (default port 1883), `mqtts` (default port 8883, the same
handler with `ConnectTarget.UseTls` true)

**Seam:** `IConnector` (ADR-0005). The handler asks it for one connection per transfer
and disposes that connection itself. The client identifier's random eight characters
come from a constructor parameter, so tests fix them.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnector`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

The source of truth for behaviour is curl 8.21.0's `lib/mqtt.c`, plus measurements
against the local curl 8.21.0; the tests name each measured case.

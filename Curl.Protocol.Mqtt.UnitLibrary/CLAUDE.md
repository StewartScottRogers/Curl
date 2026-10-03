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

Curl's own diagnostic log (`--log-level`, ADR-0222, BL-928): `MqttDiagnosticLog` writes
component `mqtt` from `ITransferContext.DiagnosticLog` - the failure that ends a transfer
as `error` with its `CurlExitCode`, a packet the session passes over (a body taken as the
CONNACK whatever its type, an empty packet ignored, a body left unread, a packet not
handled) as `warning`, the SUBSCRIBE or PUBLISH line, the CONNACK return code, the
SUBSCRIBE or PUBLISH done and the transfer end with bytes and ms as `info`, and each
packet's type and remaining length, sent and received, as `verbose`. No packet body is
logged, so the CONNECT's user name and password never are; the connect target carries the
log on.

`-v` and `--trace` (BL-935): after connecting, `MqttSession` reports to
`ITransferContext.Events` `Using client id '...'`, each packet sent as a header block,
each fixed header byte (`MqttPacketReader`) and each CONNACK or SUBACK body as header
blocks received, `Remaining length: N bytes` and each PUBLISH body slice as data, curl's
`mqtt_doing: state [N]` lines (one extra `state [0]` straight after the CONNECT), `Got
DISCONNECT`, `Received ping response.`, `server disconnected` and `State not handled yet`;
then `MqttProtocolHandler` reports the failure's message unless curl prints it without
`failf` (`MqttTransferMessages.IsStrerrorText`), then `Error 55 sending MQTT CONNECT
request` when the CONNECT could not be sent (`MqttTransferException.FollowingLine`,
BL-1229), and `closing connection #N` after exit 23 or `shutting down connection #N` after
anything else. A failed send is exit 55 `Send failure: Connection was reset` for a reset
and `Failed sending data to the peer` otherwise.

Keep-alive (BL-1116): while a packet's first byte is awaited, `MqttSession` races the
read (`MqttPacketReader.WhenFirstByteReadyAsync`, which keeps the read it starts for the
next fixed header) against a 60.001-second delay on `ITransferContext.TimeProvider`; if
the delay wins and no PINGREQ is outstanding it sends `C0 00` and reports
`mqtt_ping: sent ping request.`, as curl's `mqtt_ping` does with the default 60000 ms
upkeep interval. A PINGRESP clears the outstanding PINGREQ. The clock is read only when a
wait begins.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnector`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

The source of truth for behaviour is curl 8.21.0's `lib/mqtt.c`, plus measurements
against the local curl 8.21.0; the tests name each measured case.

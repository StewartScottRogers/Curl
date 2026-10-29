# ADR-1049 — The MQTT 5 client follows curl's mqtt:// flow and ends a failure reason code as a weird server reply

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4243.

## Context

BL-4243 adds `Mqtt5ProtocolHandler` to `Curl.Extension.Mqtt5.UnitLibrary` (ADR-0999) for `mqtt5://` and `mqtt5s://`,
on BL-4242's codec (ADR-1044). Upstream curl has no MQTT 5, so its `mqtt://` handler (MQTT 3.1.1, `lib/mqtt.c`, ported
as `Curl.Protocol.Mqtt.UnitLibrary`) is the model for what the user sees, and OASIS MQTT Version 5.0 for the wire.
MQTT 5 adds reason codes to CONNACK, SUBACK and DISCONNECT, which curl's handler never meets.

## Decision

- **curl's flow.** CONNECT (clean start, the random client identifier `curl` and eight letters or digits as curl
  makes it), CONNACK, then with a request body (`-d`) PUBLISH at QoS 0 and DISCONNECT (Normal disconnection);
  without one SUBSCRIBE (packet identifier 1, maximum QoS 0), SUBACK, then each PUBLISH's payload written to the
  output until the server sends DISCONNECT. The topic is the URL path after the first '/', URL-decoded; a shared
  subscription path `$share/name/filter` is subscribed as given. `--user` fills User Name, and Password when the
  password is not empty.
- **Only the payload is written.** curl's `mqtt://` writes a PUBLISH's whole variable part (topic length, topic,
  payload). In MQTT 5 that part also carries properties, which would put binary noise before each message, so the
  MQTT 5 schemes write the payload alone: what a subscriber wants, and what `-d` sent.
- **Exit codes, as curl's `mqtt://` gives the equivalent failure.**

  | Failure | Exit code | Message |
  |---|---|---|
  | CONNACK, SUBACK or DISCONNECT carries a failure reason code (0x80 and above) | 8 `CURLE_WEIRD_SERVER_REPLY` (curl's for a refused CONNACK or a bad SUBACK) | `MQTT 5 <packet> reason code 0x<hex> <name>`, the name as `Mqtt5ReasonCode` spells it |
  | Another packet where CONNACK or SUBACK is due, or anything but PUBLISH and DISCONNECT while subscribed | 8 | `Weird server reply` |
  | A packet from the server that breaks the wire format | 8 | `Weird server reply: malformed MQTT 5 packet: <reason>` |
  | The server closes the connection while a packet is due | 56 `CURLE_RECV_ERROR` | `Connection disconnected` (curl's text) |
  | A read or a write on the connection fails | 56 / 55 | curl's `Failure when receiving data from the peer` / `Failed sending data to the peer` |
  | The output cannot be written | 23 `CURLE_WRITE_ERROR` | curl's `Failure writing output to destination, passed N returned 0` |
  | No topic in the URL | 3 `CURLE_URL_MALFORMAT` | curl's `No MQTT topic found. Forgot to URL encode it?` |
  | A topic MQTT 5 refuses (a wildcard in a published topic name, a bad filter) | 3 | `Invalid MQTT 5 topic: <reason>`, before anything is sent |

  A DISCONNECT with Normal disconnection ends a subscription successfully, as curl's "Got DISCONNECT" does.
- **Keep Alive 0, and the broker's Server Keep Alive honoured.** CONNECT asks for no keep-alive. When CONNACK
  returns a Server Keep Alive N above 0, the client must use it (section 3.2.2.3.14), so while it waits for messages
  `Mqtt5ClientFlow` sends a PINGREQ every N/2 seconds on the transfer's `TimeProvider`, counted from the last PINGREQ
  whatever the server sends meanwhile, since Keep Alive counts only what the client sends (section 3.1.2.10). Half
  leaves room for the network before the broker's 1.5 N limit. PINGRESP is accepted and ignored; the client does not
  time out a missing one, since the broker closing the connection already ends the transfer with a receive error
  (BL-4261).
- **The flow takes its transport.** `Mqtt5ClientFlow` speaks over any `IConnection`; the handler gets a TCP or TLS
  one from the connector (`ConnectTarget.UseTls` for `mqtt5s://`, port 8883; 1883 otherwise), and BL-4244 can pass a
  WebSocket-carrying one for `mqtt5+ws://` without changing the flow. The handler is not registered here: BL-4115
  does that.

## Consequences

The MQTT 5 schemes behave like `mqtt://` in the cases they share and give scripts the same exit codes; a broker's
reason code is named in the error, so a refusal can be told apart without `-v`.

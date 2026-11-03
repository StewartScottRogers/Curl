# ADR-1201: The mqtt5:// client answers a Malformed Packet or a Protocol Error with DISCONNECT 0x81 or 0x82

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4563)

## Context

OASIS MQTT Version 5.0 section 4.13.1 says a client that reads a Malformed Packet or a Protocol Error SHOULD send a
DISCONNECT carrying the reason code (0x81 Malformed Packet, 0x82 Protocol Error) before it closes the connection;
sections 3.14.1 and 3.15 name DISCONNECT and AUTH with reserved flags, and an AUTH the client did not ask for, as such
faults. The `mqtt5://` client closed without sending anything. curl's own `mqtt://` speaks only MQTT 3.1.1, which has
no reason codes, so there is no curl behaviour to match: the extension follows its specification (ADR-0999).

## Decision

`Mqtt5ClientException` carries an optional DISCONNECT reason code. `Mqtt5PacketChannel` gives a packet that breaks the
wire format the reason code `Mqtt5MalformedPacketException` names (0x81, or the more specific code the reader found),
and `Mqtt5ClientFlow` gives an unexpected packet (an AUTH, a second CONNACK, any packet out of turn) Protocol Error
0x82. `Mqtt5ClientFlow.RunAsync` sends `DISCONNECT` with that code before the failure ends the transfer, also before
CONNACK, since section 3.14.0's rule against a DISCONNECT before CONNACK binds only the server. The send is best
effort: when it fails the transfer still ends with the original exit code and message. A refusal by reason code
(a failing CONNACK, SUBACK or DISCONNECT) is not a fault in the stream and sends no DISCONNECT.

## Consequences

Exit codes and stderr do not change (exit 8, `Weird server reply...`). Brokers see why the client closed. The
conformance cases `Mqtt5:MQTT-3.14.1-1`, `Mqtt5:MQTT-3.15.1-1`, `Mqtt5:MQTT5-3.15-1` and `Mqtt5:MQTT5-4.13.1-1` pass.

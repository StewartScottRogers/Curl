# ADR-1197: The `mqtt5://` client acknowledges received QoS 1 and 2, refuses a Topic Alias it never allowed and closes on a missing PINGRESP

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4485 (measured), BL-4580, BL-4581, BL-4582 (fixes)
- Decided by Claude under Stewart's delegation

## Context

BL-4485 measured curl's `mqtt5://` PUBLISH, QoS and SUBSCRIBE handling against OASIS MQTT Version 5.0
([Mqtt5-PublishSubscribe.md](../../Conformance/Extended/Mqtt5-PublishSubscribe.md)). `mqtt5://` is an extension
(ADR-0999), so there is no upstream curl to match; the specification decides. Three client behaviours fail it:
curl writes a QoS 1 or QoS 2 PUBLISH without acknowledging it (MQTT-4.3.2-4, MQTT-4.3.3-8, MQTT-4.3.3-11,
MQTT-4.6.0-2, MQTT-4.6.0-3), accepts a Topic Alias although its CONNECT sends no Topic Alias Maximum (section
3.3.2.3.4), and keeps waiting when a PINGREQ goes unanswered (section 3.1.2.10, a SHOULD).

## Decision

- A received QoS 1 PUBLISH is answered with PUBACK, and a QoS 2 PUBLISH with PUBREC and its PUBREL with PUBCOMP, each
  in the order received and in the short form (Reason Code 0x00 and no properties left out: `40 02 id`, `50 02 id`,
  `70 02 id`). curl still subscribes at QoS 0; a server that sends more is answered rather than left hanging.
- A PUBLISH carrying a Topic Alias, which curl never allows, is a Protocol Error: curl writes nothing of it, sends
  DISCONNECT 0x94 Topic Alias invalid and ends with exit 8.
- "A reasonable amount of time" for a PINGRESP is one ping interval (half the Server Keep Alive): when the next
  PINGREQ falls due with the last one unanswered, curl sends DISCONNECT 0x8D Keep Alive timeout and ends with exit 8.

## Consequences

The cases stay in `KnownFailures.txt` under BL-4580, BL-4581 and BL-4582 until each fix lands; each fix adds a
focused unit test in `Curl.Extended.Protocol.Mqtt5.UnitTests` (ADR-0482).

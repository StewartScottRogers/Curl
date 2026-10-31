# ADR-1044 — The MQTT 5 codec checks the wire format both ways with one exception

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4242.

## Context

BL-4115 adds an MQTT 5 client as the extension `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (ADR-0999). Upstream curl speaks
only MQTT 3.1.1 (`mqtt://`), so OASIS MQTT Version 5.0 (7 March 2019) decides. BL-4242 is the first of its parts:
the data types (section 1.5), fixed header (2.1), properties (2.2.2), reason codes (2.4), every control packet
(chapter 3), the topic rules (4.7, 4.8.2) and malformed packets (4.13), as pure byte-level code.

## Decision

- **One exception, both directions.** Every fault in the wire format raises `Mqtt5MalformedPacketException`, carrying
  the reason code a DISCONNECT for it would send: Malformed Packet (0x81) for what section 4.13 and the "is a
  Malformed Packet" sentences name, Protocol Error (0x82) for "is a Protocol Error", Topic Name invalid (0x90), Topic
  Filter invalid (0x8F), Topic Alias invalid (0x94) and Unsupported Protocol Version (0x84) where those fit. A packet
  about to be written is checked by the same rules and fails the same way, so the client never sends what it would
  refuse to read. Only a caller error that is not about the wire (a property built for the wrong data type, a packet
  class given another class's type) is an `ArgumentException`.
- **The receiver's MUSTs, not its SHOULDs.** A UTF-8 Encoded String is refused for ill-formed UTF-8 (which covers
  surrogate code points) and U+0000; control characters and non-characters, which section 1.5.4 lets a receiver
  accept, are accepted. A Variable Byte Integer longer than four bytes or longer than its value needs is malformed.
- **Properties are checked against table 2-4.** An identifier the table does not list, or one it does not allow in
  that packet (or in the Will Properties), is a Malformed Packet (section 2.2.2.2); a second copy of a property is a
  Protocol Error except User Property anywhere and Subscription Identifier in PUBLISH.
- **Shortened forms are written short.** PUBACK to PUBCOMP, DISCONNECT and AUTH leave out the reason code and
  properties when they are Success and none, and the properties when there are none, as sections 3.4.2.1, 3.14.2.1
  and 3.15.2.1 allow; reading accepts every length.

## Consequences

BL-4243's client flow reads framed packets with `Mqtt5PacketReader.TryRead` and maps a caught
`Mqtt5MalformedPacketException` to its DISCONNECT and exit code. No registration yet: BL-4115 adds it.

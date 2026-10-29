---
id: BL-4242
title: Extension Mqtt5: projects and MQTT 5 control packet wire format
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4092]
touches: [Curl.Extension.Mqtt5.UnitLibrary, Curl.Extension.Mqtt5.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4242 — Extension Mqtt5: projects and MQTT 5 control packet wire format

## Goal

`Curl.Extension.Mqtt5.UnitLibrary` encodes and decodes every MQTT 5 control packet a publish/subscribe client sends or receives, with properties and reason codes, as pure byte-level code with no I/O.

## Context

Split from BL-4115 (`mqtt5://`, `mqtt5s://`, `mqtt5+ws://`, extension 14 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: OASIS MQTT Version 5.0 (7 March 2019). Reference only `Curl.Protocol.Abstractions.UnitLibrary` and the shared layers; never `Curl.Protocol.Mqtt.UnitLibrary` (curl's 3.1.1 `mqtt://`) or another extension. Use the `new-project` skill; the slnx line is one single-line `<Folder Name="/Extensions/Mqtt5/">` directly under the marker comment (ADR-1003). Pattern to follow: BL-4218 (SignalR wire format). Put the code in `Packets/` (and `Encoding/` for data types), so BL-4243 (`Client/`) and BL-4244 (`WebSocket/`) work in disjoint folders. Do not register the extension in any catalogue here: BL-4115 does that.

## Acceptance criteria

- [x] `Curl.Extension.Mqtt5.UnitLibrary` and `Curl.Extension.Mqtt5.UnitTests` exist in `/Extensions/Mqtt5/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [x] Data types (section 1.5: Two Byte and Four Byte Integer, UTF-8 Encoded String with its section 1.5.4 restrictions, Variable Byte Integer with its 4-byte limit, Binary Data, UTF-8 String Pair) are encoded and decoded; unit tests use the specification's own Variable Byte Integer boundary values.
- [x] The fixed header (section 2.1) and properties (section 2.2.2, every identifier in table 2-4 with its type; a second copy of a property that may appear once is a Protocol Error) are encoded and decoded.
- [x] CONNECT (3.1: clean start, keep alive, will, user name, password, session expiry, receive maximum), CONNACK (3.2), PUBLISH (3.3: QoS 0/1/2, retain, topic alias), PUBACK/PUBREC/PUBREL/PUBCOMP (3.4-3.7), SUBSCRIBE (3.8 with subscription options No Local, Retain As Published, Retain Handling), SUBACK (3.9), UNSUBSCRIBE/UNSUBACK (3.10-3.11), PINGREQ/PINGRESP (3.12-3.13), DISCONNECT (3.14) and AUTH (3.15) are written and read; unit tests pin recorded bytes per section.
- [x] Reason codes (section 2.4, table 2-6) have names and their success/failure split; a shared subscription filter `$share/{ShareName}/{filter}` (section 4.8.2) is validated, including the rules on ShareName; topic name and filter wildcard rules (section 4.7) are checked.
- [x] A malformed packet (section 4.13: bad remaining length, truncated field, invalid UTF-8, unknown packet type or property) raises one named exception type carrying the MQTT reason code, never an unrelated exception.
- [x] The library meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4115's lane 6 prerequisite pass. BL-4243 then adds the client flow, BL-4244 the WebSocket transport, BL-4115 the registration, description, ADR and `CURL_EXTENSIONS=0` test.
- Lane 5 ran the `feature` stages itself rather than through sub-agents, to stay inside the cost cap: plan, tests,
  code, verify. Decisions in ADR-1044: one `Mqtt5MalformedPacketException` carrying the reason code for every wire
  fault, read or about to be written; the receiver's section 1.5.4 MUSTs only; properties checked against table 2-4
  per packet and for the Will Properties; shortened reason/property forms written short.
- `Encoding/` holds the data types (`Mqtt5Writer`, `Mqtt5Reader`, `Mqtt5VariableByteInteger`, `Mqtt5Utf8String`);
  `Packets/` the fixed header, properties, reason codes, topic rules, one class per packet (PUBACK to PUBCOMP share
  `Mqtt5PublishResponse`, SUBACK and UNSUBACK `Mqtt5SubscriptionAcknowledgement`, PINGREQ and PINGRESP `Mqtt5Ping`)
  and `Mqtt5PacketReader` (`TryRead` for a stream, `Read` for one whole packet). Recorded bytes per section are worked
  by hand from the specification; there is no real curl to measure for MQTT 5.
- Tests are grouped by area (`Mqtt5DataTypeTests`, `Mqtt5PropertyCodecTests`, `Mqtt5TopicsTests`,
  `Mqtt5PacketReaderTests`), 127 of them, all platform-neutral (pure bytes, no file system or OS text).
- Measure-CodeQuality.ps1 -Library Curl.Extension.Mqtt5.UnitLibrary: 100% line, 100% branch, 0 failing members,
  highest complexity 10 (after splitting `Mqtt5Publish.Validate`, which was 14). `dotnet build` clean; fast tests
  green in all 63 test projects; `VerifyExtensionIsolation` passes in that build.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e8baf0a7 c0741cb9 1a5c5a50 7f96b952 d5bed3a7 8be0418c f4f7f560 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.Mqtt5.UnitLibrary encodes and decodes every MQTT 5 control packet with properties and reason codes, at all four quality gates

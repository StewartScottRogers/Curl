---
id: BL-4256
title: Extension Amqp1: AMQP 1.0 frames, performatives, SASL frames and message sections
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4258]
touches: [Curl.Extension.Amqp1.UnitLibrary/Frames, Curl.Extension.Amqp1.UnitTests/Frames, Curl.Extension.Amqp1.UnitLibrary/Types/Amqp1Reader.cs, Curl.Extension.Amqp1.UnitLibrary/Amqp1MalformedEncodingException.cs, Curl.Extension.Amqp1.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4256 — Extension Amqp1: AMQP 1.0 frames, performatives, SASL frames and message sections

## Goal

`Curl.Extension.Amqp1.UnitLibrary` writes and reads the AMQP 1.0 protocol headers, frames, every performative, the SASL frames and the message sections a sending or receiving client needs, as pure byte-level code with no I/O.

## Context

Split from BL-4118 (AMQP 1.0, extension 17 of 63) for size by its first pass. Builds on BL-4258's type system (`Types/`). Specification: OASIS AMQP Version 1.0, Part 2 "Transport", Part 3 "Messaging" and Part 5 "Security". Put the code in `Frames/` (and its tests in `Frames/`), so BL-4257 (`Client/`) works in a disjoint folder. Never reference a `Curl.Protocol.*` library or another extension. Do not register anything here: BL-4118 does that.

## Acceptance criteria

- [x] The protocol headers `AMQP 0 1.0.0` and `AMQP 3 1.0.0` (Part 2 section 2.2, Part 5 section 5.3.1) are written and recognised, and a mismatched header is reported.
- [x] The frame layout of Part 2 section 2.3 (SIZE, DOFF, TYPE 0x00 AMQP and 0x01 SASL, channel, extended header skipped, empty frame as heartbeat) is written and read.
- [x] open, begin, attach, flow, transfer, disposition, detach, end and close (Part 2 section 2.7) with their descriptor codes, the error type (2.8.14) and delivery states accepted, rejected, released, modified and received (Part 3 section 3.4) are written and read as composite lists; unit tests pin recorded bytes per section.
- [x] sasl-mechanisms, sasl-init, sasl-challenge, sasl-response and sasl-outcome (Part 5 section 5.3.3) are written and read, with the PLAIN (RFC 4616) and ANONYMOUS (RFC 4505) initial responses.
- [x] Message sections header, properties, application-properties, data, amqp-value and amqp-sequence (Part 3 section 3.2) are written into a transfer payload and read back from one or more transfer frames.
- [x] A malformed frame (size below 8, DOFF below 2, unknown descriptor, missing mandatory field) raises the library's one named exception type.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4118's lane 3 prerequisite pass.
- Touches widened (no other task in Doing on origin/work/dark-factory names them): `Types/Amqp1Reader.cs` gains a `Position` property so a frame body splits into performative and payload; `Amqp1MalformedEncodingException.cs` doc comment now covers frames, as it is the library's one exception; the project `CLAUDE.md` describes `Frames/`.
- Design (ADR-1058): one catalogue `Amqp1CompositeType` (name, descriptor code, kind, field names with mandatory marks) and one value type `Amqp1Composite`, rather than a record per performative. Written with numeric descriptors, read with numeric or symbolic ones. Frames read from a buffer (`Amqp1FrameCodec.TryRead`), so BL-4257 owns all I/O and the max-frame-size limit. Section order is not checked (a client reads what a broker sends).
- No AMQP 1.0 broker on this machine and upstream curl has no AMQP, so the pinned bytes are derived by hand from the specification encodings (ADR-1053 smallest encodings), not recorded from a broker.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Amqp1.UnitLibrary`: 100% line, 100% branch, 0 failing members, max complexity 10. dotnet build clean; fast tests green (66 test assemblies passed, 0 failed); Amqp1 tests 184.
- CI on Linux and macOS: the tests are pure byte-level with no paths, files or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9920929e da302431 1335109a 6d7c40b6 dbcb64cf 3cfbad74 14de81cc.
- 2026-10-29: Doing -> Done. Curl.Extension.Amqp1 writes and reads AMQP 1.0 protocol headers, frames, every performative, delivery states, SASL frames and message sections

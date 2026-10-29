---
id: BL-4164
title: Create Curl.Protobuf with the Protocol Buffers wire format codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protobuf.UnitLibrary, Curl.Protobuf.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4164 — Create Curl.Protobuf with the Protocol Buffers wire format codec

## Goal

A new hand-built `Curl.Protobuf.UnitLibrary` reads and writes the Protocol Buffers wire format: varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields.

## Context

First of three parts split from BL-4099 (wire format, then `.proto` parsing BL-4165, then JSON mapping BL-4166). A shared Wire layer for the gRPC, gRPC-Web and Connect extensions (ADR-0999; extensions may not reference one another, so shared code lives in a layer like `Curl.Zstandard`). Create the project and its `Curl.Protobuf.UnitTests` with the `new-project` skill, placed in `/Wire/Protobuf/` in `Curl.slnx`. Base class library only, native AOT safe (no reflection). Put the codec under `Wire/` in the library (and tests under `Wire/` in the test project) so BL-4165 and BL-4166 can add `Descriptors/` and `Json/` beside it. Malformed input is reported as a typed result or error (for example a `ProtobufDecodeError` enum on a `TryRead...` result), never an exception escaping.

## Acceptance criteria

- [x] `Curl.Protobuf.UnitLibrary` and `Curl.Protobuf.UnitTests` exist and are listed in `/Wire/Protobuf/` in `Curl.slnx`.
- [x] Varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields round-trip, each checked against byte vectors from the Protocol Buffers encoding specification.
- [x] Malformed input (truncated varint, varint longer than 10 bytes, length past end, nested groups or messages deeper than 100) fails with a typed error, never an exception escaping; unit tests.
- [x] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Pipeline `feature` run directly by this lane (one library, one codec; no architect stage needed). API: `ProtobufReader` (`TryRead...` returning `ProtobufDecodeError`), `ProtobufWriter`, `ProtobufField` (unknown fields and groups, written back byte for byte), `ProtobufZigZag`, `ProtobufWireType`, namespace `Curl.Protobuf.Wire`. Decisions in ADR-1005: bits past the 64th on a tenth varint byte are discarded (the specification), only a continuation bit there is `VarintTooLong`; nesting limit 100 (reference default), a depth-100 reader reads its fields but opens no deeper level.
- Vectors from protobuf.dev encoding guide: 08 96 01, 12 07 "testing", 1a 03 08 96 01, packed 22 06 03 8e 02 9e a7 05, int32 -1 as ten bytes, the ZigZag table.
- Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary: 100% line, 100% branch, 45 members, 0 failing, worst CRAP 10. Build clean, fast tests green (48 new). Tests are pure byte computation, platform-neutral, so CI on Linux and macOS is met by that.

## Log

- 2026-10-29: Created (split from BL-4099).
- 2026-10-29: Backlog -> Doing. Criteria at claim: d8bdab76 20879125 64aad06d 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Protobuf.UnitLibrary reads and writes the Protocol Buffers wire format with typed decode errors

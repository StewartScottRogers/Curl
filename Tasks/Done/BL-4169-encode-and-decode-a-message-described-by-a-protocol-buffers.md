---
id: BL-4169
title: Encode and decode a message described by a Protocol Buffers descriptor through the wire codec in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4170]
touches: [Curl.Protobuf.UnitLibrary/DynamicMessages, Curl.Protobuf.UnitTests/DynamicMessages, Curl.Protobuf.UnitLibrary/Wire/ProtobufDecodeError.cs, Curl.Protobuf.UnitLibrary/Wire/ProtobufReader.cs, Curl.Protobuf.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4169 — Encode and decode a message described by a Protocol Buffers descriptor through the wire codec in Curl.Protobuf

## Goal

A message described by a BL-4170 descriptor is encoded to and decoded from Protocol Buffers binary through the BL-4164 wire codec.

## Context

Third of three parts split from BL-4165. Code under `DynamicMessages/` in the library, tests under `DynamicMessages/` in `Curl.Protobuf.UnitTests`. A dynamic message holds field values keyed by descriptor field. Encoding follows https://protobuf.dev/programming-guides/encoding/: every scalar type, packed repeated scalars (accepting unpacked on decode), nested messages, enums, `map<,>` as repeated entry messages, `oneof` (last one wins), proto3 implicit presence (default values not written) and `optional` explicit presence, unknown fields kept and written back. Decode errors come back as the wire codec's typed error, never an exception escaping. Base class library only, native AOT safe.

## Acceptance criteria

- [x] A message described by a descriptor is encoded to and decoded from binary through the wire codec, checked against byte vectors from the Protocol Buffers encoding specification (including 150 as `08 96 01`, the string `12 07 74 65 73 74 69 6e 67`, the nested `1a 03 08 96 01` and the packed `32 06 03 8e 02 9e a7 05`).
- [x] Malformed input fails with a typed decode error, never an exception escaping; unit tests.
- [x] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `DynamicTypeCatalogue` resolves a set of files' named types (innermost scope out) into `DynamicMessageType`/`DynamicField`; `DynamicMessage` holds boxed values by field number plus unknown fields; `DynamicMessageEncoder` and `DynamicMessageDecoder` go through `ProtobufWriter`/`ProtobufReader`. Decisions in ADR-1106.
- Touches widened to `Wire/ProtobufDecodeError.cs` (new `InvalidUtf8`: proto3 rejects a non-UTF-8 string, and the task asks for the wire codec's typed error), `Wire/ProtobufReader.cs` (public `TryReadFieldValue`, so a field found unknown after its tag is kept and nested messages keep the reader's depth limit) and the library's `CLAUDE.md` (documents `DynamicMessages/`). No task in Doing on origin/work/dark-factory names them (BL-4168 holds `Descriptors/` only).
- Default taken: a field with the wrong wire type for its descriptor is kept as an unknown field, as protoc's C++ runtime does; map entries always write key and value.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet build Curl.slnx` 0 warnings; fast tests 90 assemblies passed, 198 in Curl.Protobuf.UnitTests. Tests are pure byte vectors, platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c2d40f59 53f6b82b 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Done. A message described by a .proto descriptor encodes to and decodes from Protocol Buffers binary through the wire codec, malformed input a typed ProtobufDecodeError

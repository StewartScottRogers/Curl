---
id: BL-4166
title: Convert JSON to and from Protocol Buffers binary by descriptor in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4164, BL-4165]
touches: [Curl.Protobuf.UnitLibrary/Json, Curl.Protobuf.UnitTests/Json]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4166 — Convert JSON to and from Protocol Buffers binary by descriptor in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` converts JSON to Protocol Buffers binary and back, by a BL-4165 descriptor, per the proto3 JSON mapping.

## Context

Third of three parts split from BL-4099. Builds on BL-4164's wire codec and BL-4165's descriptors. Code goes under `Json/` in the library and its tests under `Json/` in `Curl.Protobuf.UnitTests`. Use `System.Text.Json`'s `Utf8JsonReader`/`Utf8JsonWriter` (BCL, AOT safe; no reflection serializer). Follow the proto3 JSON mapping: lowerCamelCase field names (accept the original name too), 64-bit integers as strings, bytes as base64, enums by name, `NaN`/`Infinity`, maps as objects, default values omitted, unknown JSON fields rejected.

## Acceptance criteria

- [x] JSON is converted to and from binary by descriptor per the proto3 JSON mapping, with unit tests for each rule listed under Context.
- [x] Malformed JSON, a field of the wrong type and nesting deeper than 100 fail with a typed error, never an exception escaping; unit tests.
- [x] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Built on BL-4169's `DynamicMessage` (the descriptor resolved to a `DynamicMessageType`), so `Json/` reads
  JSON into a dynamic message and writes one out; the binary side is `DynamicMessageEncoder`/`Decoder`.
  `ProtobufJsonConverter` (JSON to binary and back), `ProtobufJsonParser`, `ProtobufJsonFormatter`,
  `ProtobufJsonNames`, internal `ProtobufJsonScalars`, and the typed `ProtobufJsonError`. Decisions in ADR-1120.
- Delivered directly rather than through the `/feature` subagent stages: one library, one folder, and
  the design follows the dynamic message model already in place; the lane's cost rules favour doing it inline.
- `JsonDocument` reports invalid UTF-8 and lone escaped surrogates only when a string is read, as an
  `InvalidOperationException`; the parser maps it to `MalformedJson`.
- `json_name`'s option value is held quoted by the descriptor parser; `ProtobufJsonNames` strips the quotes.
- Well-known types' special JSON forms and rejecting a oneof given twice are left out (ADR-1120); no task
  filed, since no extension uses them yet.
- `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary`: 100% line, 100% branch, highest complexity 10,
  0 failing members. 327 tests in `Curl.Protobuf.UnitTests` pass; the fast run is green.
- The tests are platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is expected green.

## Log

- 2026-10-29: Created (split from BL-4099).
- 2026-10-29: Backlog -> Doing. Criteria at claim: de6465f3 a3ed0077 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Done. JSON converts to and from Protocol Buffers binary by descriptor under the proto3 JSON mapping, with typed errors

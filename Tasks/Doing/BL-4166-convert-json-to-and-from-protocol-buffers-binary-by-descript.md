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
completed:
---
# BL-4166 — Convert JSON to and from Protocol Buffers binary by descriptor in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` converts JSON to Protocol Buffers binary and back, by a BL-4165 descriptor, per the proto3 JSON mapping.

## Context

Third of three parts split from BL-4099. Builds on BL-4164's wire codec and BL-4165's descriptors. Code goes under `Json/` in the library and its tests under `Json/` in `Curl.Protobuf.UnitTests`. Use `System.Text.Json`'s `Utf8JsonReader`/`Utf8JsonWriter` (BCL, AOT safe; no reflection serializer). Follow the proto3 JSON mapping: lowerCamelCase field names (accept the original name too), 64-bit integers as strings, bytes as base64, enums by name, `NaN`/`Infinity`, maps as objects, default values omitted, unknown JSON fields rejected.

## Acceptance criteria

- [ ] JSON is converted to and from binary by descriptor per the proto3 JSON mapping, with unit tests for each rule listed under Context.
- [ ] Malformed JSON, a field of the wrong type and nesting deeper than 100 fail with a typed error, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created (split from BL-4099).
- 2026-10-29: Backlog -> Doing. Criteria at claim: de6465f3 a3ed0077 7ae6d7ff 3d836d5e.

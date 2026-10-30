---
id: BL-4165
title: Read proto3 .proto files into Protocol Buffers descriptors in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4164, BL-4170, BL-4168, BL-4169]
touches: [Curl.Protobuf.UnitLibrary/Descriptors, Curl.Protobuf.UnitTests/Descriptors]
requirement: none
created: 2026-10-29
completed:
---
# BL-4165 — Read proto3 .proto files into Protocol Buffers descriptors in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` reads a proto3 `.proto` file's messages, enums and services into run-time descriptors, and encodes and decodes a message described by them through the BL-4164 wire codec.

## Context

Second of three parts split from BL-4099. Builds on BL-4164's wire codec. Code goes under `Descriptors/` in the library and its tests under `Descriptors/` in `Curl.Protobuf.UnitTests`. Base class library only, no parser library, native AOT safe. Cover the proto3 language specification: `syntax`, `package`, `import` (resolved through an injected file reader), `option`, messages (nested), enums, `oneof`, `map<,>`, `repeated`, `optional`, `reserved`, the scalar types, and services with `rpc` (including `stream`). Syntax errors come back as a typed error with line and column, never an exception escaping.

## Acceptance criteria

- [ ] A proto3 `.proto` file's messages, enums and services are read into descriptors, with unit tests for each construct listed under Context.
- [ ] A message described by a descriptor is encoded to and decoded from binary through the wire codec, checked against byte vectors from the Protocol Buffers encoding specification.
- [ ] A malformed `.proto` file fails with a typed error naming line and column, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 5): Split for size before any code: the parser, services and imports, and the descriptor-driven codec each fill a run with the 100% coverage gate. BL-4170 (messages, enums, fields, errors), BL-4168 (services, rpc, imports), BL-4169 (dynamic message codec, under `DynamicMessages/`). Once all three are Done this task only verifies the criteria and moves to Done.

## Log

- 2026-10-29: Created (split from BL-4099).
- 2026-10-29: Backlog -> Doing. Criteria at claim: db2edfd5 ef33a4ad 05c7963a 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4170, BL-4168 and BL-4169; waits on them
- 2026-10-29: Backlog -> Doing. Criteria at claim: db2edfd5 ef33a4ad 05c7963a 7ae6d7ff 3d836d5e.

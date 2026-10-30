---
id: BL-4099
title: Build a Protocol Buffers codec as a shared Wire layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4164, BL-4165, BL-4166]
touches: [Curl.Protobuf.UnitLibrary, Curl.Protobuf.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4099 — Build a Protocol Buffers codec as a shared Wire layer

## Goal

A hand-built `Curl.Protobuf.UnitLibrary` encodes and decodes Protocol Buffers messages (wire format, and messages described at run time from a descriptor) for gRPC, gRPC-Web and Connect.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extensions may not reference one another, so code three of them share lives in a shared layer, like `Curl.Zstandard`. Base class library only, native AOT safe (no reflection). Place it in `/Wire/Protobuf/` in `Curl.slnx`.

## Acceptance criteria

- [x] Varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields round-trip, each checked against byte vectors from the Protocol Buffers encoding specification.
- [x] A `.proto` file's messages and services are read into descriptors (proto3 syntax), and JSON is converted to and from binary by descriptor per the proto3 JSON mapping.
- [x] Malformed input (truncated varint, length past end, nesting deeper than 100) fails with a typed error, never an exception escaping; unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split if larger than one run: wire format first, then `.proto` parsing, then JSON mapping.

- 2026-10-29 (lane 4): Split for size, as the Notes above anticipated: a hand-built wire codec, a proto3 parser and the JSON mapping, each to 100% coverage, are three runs. Filed BL-4164 (create the project and the wire format codec), BL-4165 (`.proto` to descriptors, depends on BL-4164) and BL-4166 (JSON mapping, depends on both). Each subtask names its own subfolder (`Wire/`, `Descriptors/`, `Json/`) in touches. Once all three are Done, this task only verifies its criteria against the finished library and closes.

- 2026-10-29 (lane 5): Verified only, no code changed. BL-4164 (wire codec with the specification's byte vectors and typed malformed-input errors including depth over 100), BL-4165 (proto3 messages and services to descriptors) and BL-4166 (proto3 JSON mapping) are Done. `dotnet build` clean (0 errors); every fast test project passes. `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary`: 100% line, 100% branch, 341 members, 0 failing, highest complexity 10; 327 tests in Curl.Protobuf.UnitTests. Tests are pure computation with no paths or OS text, so CI on Linux and macOS is covered by that.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 20879125 8627716d 72bc5ee6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4164, BL-4165 and BL-4166; waits on them
- 2026-10-29: Backlog -> Doing. Criteria at claim: 20879125 8627716d 72bc5ee6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Protobuf.UnitLibrary encodes and decodes Protocol Buffers wire format, reads proto3 files into descriptors and maps JSON by descriptor, at 100% coverage

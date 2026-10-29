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
completed:
---
# BL-4164 — Create Curl.Protobuf with the Protocol Buffers wire format codec

## Goal

A new hand-built `Curl.Protobuf.UnitLibrary` reads and writes the Protocol Buffers wire format: varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields.

## Context

First of three parts split from BL-4099 (wire format, then `.proto` parsing BL-4165, then JSON mapping BL-4166). A shared Wire layer for the gRPC, gRPC-Web and Connect extensions (ADR-0999; extensions may not reference one another, so shared code lives in a layer like `Curl.Zstandard`). Create the project and its `Curl.Protobuf.UnitTests` with the `new-project` skill, placed in `/Wire/Protobuf/` in `Curl.slnx`. Base class library only, native AOT safe (no reflection). Put the codec under `Wire/` in the library (and tests under `Wire/` in the test project) so BL-4165 and BL-4166 can add `Descriptors/` and `Json/` beside it. Malformed input is reported as a typed result or error (for example a `ProtobufDecodeError` enum on a `TryRead...` result), never an exception escaping.

## Acceptance criteria

- [ ] `Curl.Protobuf.UnitLibrary` and `Curl.Protobuf.UnitTests` exist and are listed in `/Wire/Protobuf/` in `Curl.slnx`.
- [ ] Varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields round-trip, each checked against byte vectors from the Protocol Buffers encoding specification.
- [ ] Malformed input (truncated varint, varint longer than 10 bytes, length past end, nested groups or messages deeper than 100) fails with a typed error, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created (split from BL-4099).
- 2026-10-29: Backlog -> Doing. Criteria at claim: d8bdab76 20879125 64aad06d 7ae6d7ff 3d836d5e.

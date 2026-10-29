---
id: BL-4099
title: Build a Protocol Buffers codec as a shared Wire layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091]
touches: [Curl.Protobuf.UnitLibrary, Curl.Protobuf.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4099 — Build a Protocol Buffers codec as a shared Wire layer

## Goal

A hand-built `Curl.Protobuf.UnitLibrary` encodes and decodes Protocol Buffers messages (wire format, and messages described at run time from a descriptor) for gRPC, gRPC-Web and Connect.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extensions may not reference one another, so code three of them share lives in a shared layer, like `Curl.Zstandard`. Base class library only, native AOT safe (no reflection). Place it in `/Wire/Protobuf/` in `Curl.slnx`.

## Acceptance criteria

- [ ] Varints, zigzag, fixed32/64, length-delimited fields, packed repeated fields and unknown fields round-trip, each checked against byte vectors from the Protocol Buffers encoding specification.
- [ ] A `.proto` file's messages and services are read into descriptors (proto3 syntax), and JSON is converted to and from binary by descriptor per the proto3 JSON mapping.
- [ ] Malformed input (truncated varint, length past end, nesting deeper than 100) fails with a typed error, never an exception escaping; unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split if larger than one run: wire format first, then `.proto` parsing, then JSON mapping.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 20879125 8627716d 72bc5ee6 e35aff77 3d836d5e.

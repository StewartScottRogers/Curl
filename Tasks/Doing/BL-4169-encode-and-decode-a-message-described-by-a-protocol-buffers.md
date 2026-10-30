---
id: BL-4169
title: Encode and decode a message described by a Protocol Buffers descriptor through the wire codec in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4170]
touches: [Curl.Protobuf.UnitLibrary/DynamicMessages, Curl.Protobuf.UnitTests/DynamicMessages]
requirement: none
created: 2026-10-29
completed:
---
# BL-4169 — Encode and decode a message described by a Protocol Buffers descriptor through the wire codec in Curl.Protobuf

## Goal

A message described by a BL-4170 descriptor is encoded to and decoded from Protocol Buffers binary through the BL-4164 wire codec.

## Context

Third of three parts split from BL-4165. Code under `DynamicMessages/` in the library, tests under `DynamicMessages/` in `Curl.Protobuf.UnitTests`. A dynamic message holds field values keyed by descriptor field. Encoding follows https://protobuf.dev/programming-guides/encoding/: every scalar type, packed repeated scalars (accepting unpacked on decode), nested messages, enums, `map<,>` as repeated entry messages, `oneof` (last one wins), proto3 implicit presence (default values not written) and `optional` explicit presence, unknown fields kept and written back. Decode errors come back as the wire codec's typed error, never an exception escaping. Base class library only, native AOT safe.

## Acceptance criteria

- [ ] A message described by a descriptor is encoded to and decoded from binary through the wire codec, checked against byte vectors from the Protocol Buffers encoding specification (including 150 as `08 96 01`, the string `12 07 74 65 73 74 69 6e 67`, the nested `1a 03 08 96 01` and the packed `32 06 03 8e 02 9e a7 05`).
- [ ] Malformed input fails with a typed decode error, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c2d40f59 53f6b82b 7ae6d7ff 3d836d5e.

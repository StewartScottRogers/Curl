---
id: BL-4258
title: Extension Amqp1: projects and AMQP 1.0 type system encoding
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4092]
touches: [Curl.Extension.Amqp1.UnitLibrary, Curl.Extension.Amqp1.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4258 — Extension Amqp1: projects and AMQP 1.0 type system encoding

## Goal

`Curl.Extension.Amqp1.UnitLibrary` encodes and decodes every AMQP 1.0 type a client sends or receives, as pure byte-level code with no I/O.

## Context

Split from BL-4118 (AMQP 1.0, extension 17 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: OASIS AMQP Version 1.0 (29 October 2012), Part 1 "Types". Reference only `Curl.Extension.Abstractions.UnitLibrary` (and through it `Curl.Protocol.Abstractions`); never a `Curl.Protocol.*` library or another extension, including BL-4117's `Curl.Extension.Amqp0.UnitLibrary` (AMQP 0-9-1, which owns `amqp://`/`amqps://`). Use the `new-project` skill; the slnx line is one single-line `<Folder Name="/Extensions/Amqp1/">` directly under the marker comment (ADR-1003). Pattern to follow: BL-4242 (Mqtt5 wire format). Put the code in `Types/`, so BL-4256 (`Frames/`) and BL-4257 (`Client/`) work in disjoint folders. Do not register the extension in any catalogue here: BL-4118 does that.

## Acceptance criteria

- [ ] `Curl.Extension.Amqp1.UnitLibrary` and `Curl.Extension.Amqp1.UnitTests` exist in `/Extensions/Amqp1/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] Every primitive type of Part 1 section 1.6 (null, boolean with its true/false/0x56 forms, ubyte to ulong with their smalluint/smallulong/uint0/ulong0 forms, byte to long with small forms, float, double, decimal32/64/128 as raw bytes, char, timestamp, uuid, binary vbin8/vbin32, string str8/str32, symbol sym8/sym32) is written in its smallest encoding and read from every encoding; unit tests pin the constructor bytes of section 1.6 per type.
- [ ] Compound types list (list0, list8, list32), map (map8, map32) and array (array8, array32, one element constructor) of section 1.4 and 1.6.22-1.6.25 are written and read, nested.
- [ ] Described types (section 1.3: descriptor 0x00, symbolic or numeric ulong descriptor) are written and read, so BL-4256 can build composite types (section 1.4: a described list whose trailing nulls may be omitted) on them.
- [ ] Malformed input (unknown constructor, truncated value, size or count past the buffer, invalid UTF-8 in a string, non-ASCII symbol) raises one named exception type, never an unrelated exception.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4118's lane 3 prerequisite pass.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f9e88c58 77e33d9c c6bef9a9 d45701cf e8afd933 14de81cc.

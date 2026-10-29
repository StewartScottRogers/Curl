---
id: BL-4170
title: Parse proto3 messages and enums into Protocol Buffers descriptors in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4164]
touches: [Curl.Protobuf.UnitLibrary/Descriptors, Curl.Protobuf.UnitTests/Descriptors]
requirement: none
created: 2026-10-29
completed:
---
# BL-4170 — Parse proto3 messages and enums into Protocol Buffers descriptors in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` reads proto3 `.proto` text into run-time file, message, field and enum descriptors, and malformed text fails with a typed error naming line and column.

## Context

First of three parts split from BL-4165 (itself split from BL-4099) because the whole did not fit one run. Builds on BL-4164's project. Code under `Descriptors/` in the library, tests under `Descriptors/` in `Curl.Protobuf.UnitTests`. Base class library only, a hand-written tokenizer and recursive-descent parser, native AOT safe. Covers the proto3 language specification (https://protobuf.dev/reference/protobuf/proto3-spec/): comments, `syntax`, `package`, `option` (file, message, field and enum level, kept as name/value pairs), messages (nested), enums (including `allow_alias` and negative values), fields of every scalar type, message and enum type references, `repeated`, `optional`, `oneof`, `map<,>`, `reserved` (numbers, ranges, `max`, names). Give each top-level construct its own parse method so BL-4168 can add `service`/`rpc` and `import` without rewriting the parser. Syntax errors come back as a result carrying a typed error with line and column, never an exception escaping.

## Acceptance criteria

- [ ] Proto3 text's messages (nested), enums, fields (every scalar type, `repeated`, `optional`, `oneof`, `map<,>`), `reserved`, `syntax`, `package` and `option` are read into descriptors, with a unit test for each construct.
- [ ] Malformed `.proto` text fails with a typed error naming line and column, never an exception escaping; unit tests.
- [ ] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.

---
id: BL-4168
title: Parse proto3 services, rpc streams and resolve imports through an injected file reader in Curl.Protobuf
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4170]
touches: [Curl.Protobuf.UnitLibrary/Descriptors, Curl.Protobuf.UnitTests/Descriptors]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4168 — Parse proto3 services, rpc streams and resolve imports through an injected file reader in Curl.Protobuf

## Goal

`Curl.Protobuf.UnitLibrary` reads a proto3 file's services and `rpc` methods (including `stream`) into descriptors, and resolves `import` statements through an injected file reader.

## Context

Second of three parts split from BL-4165. Extends BL-4170's parser under `Descriptors/`. Services hold methods with input and output message types, client and server `stream` flags, and the `option`s in service and method bodies. `import`, `import public` and `import weak` are resolved through an injected file-reader interface (no direct file system access), so references to imported messages and enums resolve; a missing import or an import cycle is a typed error with line and column. Base class library only, native AOT safe.

## Acceptance criteria

- [x] A proto3 file's services and `rpc` methods (unary, client, server and bidirectional `stream`) are read into descriptors; unit tests.
- [x] `import` is resolved through an injected file reader and imported types resolve; a missing import and an import cycle each fail with a typed error naming line and column; unit tests.
- [x] `Curl.Protobuf.UnitLibrary` meets the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29, lane 11: delivered directly in the session (no sub-agents, for the run's budget); design in ADR-1106.
  `ProtoServiceParser` reads `service`/`rpc` (stream either side, options in both bodies; a scalar request or
  response is a syntax error); `ProtoFileParser` reads `import`, `import public` and `import weak` into
  `ProtoImportDescriptor` (path, kind, keyword line and column). `ProtoFileSetReader.TryRead` reads a file and
  its imports through the injected `IProtoFileReader`, fails with a typed `ProtoFileError` (`FileNotFound`,
  `Syntax`, `MissingImport`, `ImportCycle`, `UnresolvedType`, `NotAMessage`), and `ProtoTypeResolver`
  resolves names innermost scope outward with `public` import visibility; `ProtoFileSet.FindType` gives each
  reference's type. Tests: `ProtoServiceParserTests`, `ProtoFileSetReaderTests`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protobuf.UnitLibrary`: 100% line, 100% branch, max complexity
  10, 0 failing members. Full build clean, fast tests green (89 test assemblies, 0 failed). The tests are
  platform-neutral (in-memory reader, no paths or OS text), which meets the Linux and macOS CI need.
- `Curl.Protobuf.UnitLibrary/CLAUDE.md` still says `Descriptors/` is to come; it is outside this task's
  touches, so it is left for a docs pass.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 564d89be 99c0bb5f 7ae6d7ff 3d836d5e.
- 2026-10-29: Doing -> Done. Proto3 services and rpc streams are read into descriptors, and imports resolve through an injected file reader with typed errors for missing imports, cycles and unresolved types

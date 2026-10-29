---
id: BL-4026
title: Commit the C# file-based app that generates CurlUrlHostNameMappingTable.cs from the Unicode 15.1 data
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs, Generate-CurlUrlHostNameMappingTable.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4026 — Commit the C# file-based app that generates CurlUrlHostNameMappingTable.cs from the Unicode 15.1 data

## Goal

`dotnet run Generate-CurlUrlHostNameMappingTable.cs` regenerates `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs` byte for byte from `IdnaMappingTable.txt`, `UnicodeData.txt` and `CompositionExclusions.txt` (Unicode 15.1).

## Context

BL-3829 (ADR-0972) committed the generated table but not the C# file-based app that wrote it, so the table cannot be regenerated or checked. The format is in the table's XML doc comments: each table is Base64 of unsigned LEB128 numbers; `Mappings` holds gap, range length less one, mapping length (0 for ignored) and the mapping's code points as zig-zag differences (mapped and `disallowed_STD3_mapped` ranges, deviations left unmapped); `CombiningClasses` holds gap, range length less one and class; `Decompositions` holds the gap less one from the previous code point, a header of part count times two plus one when the pair is a primary composite (not in `CompositionExclusions.txt`, not a singleton, not non-starter), and the parts as zig-zag differences. Read `CurlUrlHostNameMapping.ReadMappings`, `ReadCombiningClasses` and `ReadDecompositions` for the exact layout. No Python.

## Acceptance criteria

- [ ] `Generate-CurlUrlHostNameMappingTable.cs` at the repository root, run with the three Unicode 15.1 files' paths, writes a `CurlUrlHostNameMappingTable.cs` identical to the committed one.
- [ ] ADR-0972 names the app and how to run it.

## Notes

## Log

- 2026-10-28: Created by BL-3829's run.

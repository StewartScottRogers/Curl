---
id: BL-4034
title: Commit the C# file-based app that generates CurlUrlHostNameMappingTable.cs from the Unicode 15.1 data
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs, Generate-CurlUrlHostNameMappingTable.cs, Documentation/Planning/Decisions/ADR-0980-host-names-are-mapped-with-the-whole-uts-46-table-and-nfc.md]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4034 — Commit the C# file-based app that generates CurlUrlHostNameMappingTable.cs from the Unicode 15.1 data

## Goal

`dotnet run Generate-CurlUrlHostNameMappingTable.cs` regenerates `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMappingTable.cs` byte for byte from `IdnaMappingTable.txt`, `UnicodeData.txt` and `CompositionExclusions.txt` (Unicode 15.1).

## Context

BL-3829 (ADR-0980) committed the generated table but not the C# file-based app that wrote it, so the table cannot be regenerated or checked. The format is in the table's XML doc comments: each table is Base64 of unsigned LEB128 numbers; `Mappings` holds gap, range length less one, mapping length (0 for ignored) and the mapping's code points as zig-zag differences (mapped and `disallowed_STD3_mapped` ranges, deviations left unmapped); `CombiningClasses` holds gap, range length less one and class; `Decompositions` holds the gap less one from the previous code point, a header of part count times two plus one when the pair is a primary composite (not in `CompositionExclusions.txt`, not a singleton, not non-starter), and the parts as zig-zag differences. Read `CurlUrlHostNameMapping.ReadMappings`, `ReadCombiningClasses` and `ReadDecompositions` for the exact layout. No Python.

## Acceptance criteria

- [x] `Generate-CurlUrlHostNameMappingTable.cs` at the repository root, run with the three Unicode 15.1 files' paths, writes a `CurlUrlHostNameMappingTable.cs` identical to the committed one.
- [x] ADR-0980 names the app and how to run it.

## Notes

- Added ADR-0980 to touches: the second criterion edits it; no task in Doing on origin/work/dark-factory names it.
- The app parses the three files as the table's doc comments say: IDNA lines whose status is mapped, disallowed_STD3_mapped or ignored, one entry per file line as written (no merging); combining classes merged over consecutive code points of one class; canonical decompositions only (compatibility `<tag>` ones skipped), primary composite when two parts, not excluded and the character and its first part are starters.
- Checked: run with Unicode 15.1.0 files from unicode.org/Public, the output was byte-identical (`cmp`) to the committed table, both to a temp path and over the committed file (git status unchanged). Static methods sit in a class so the top-level Main stays under the complexity gate (CA1502 fails `dotnet run` otherwise).
- No production code changed, so no Measure-CodeQuality run; build clean and fast tests green.

## Log

- 2026-10-28: Created by BL-3829's run.
- 2026-10-28: Backlog -> Doing. Criteria at claim: c47fee76 452eaeed.
- 2026-10-28: Doing -> Done. Generate-CurlUrlHostNameMappingTable.cs regenerates the host name mapping table byte for byte from the Unicode 15.1 data

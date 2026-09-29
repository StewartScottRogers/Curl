---
id: BL-857
title: Create Curl.Zstandard.UnitLibrary and its tests as ADR-0185's hand-built library
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests, Curl.slnx, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-857 — Create Curl.Zstandard.UnitLibrary and its tests as ADR-0185's hand-built library

## Goal

`Curl.Zstandard.UnitLibrary` and `Curl.Zstandard.UnitTests` exist in the solution, empty but building, and `ProtocolIsolationTests` holds ADR-0185's new row of ADR-0120's table.

## Context

- ADR-0185 (BL-785) places the hand-built Zstandard decoder and XXH64 in a new hand-built library that references nothing; ADR-0120 (as amended) adds its row and lets `Curl.Tls.UnitLibrary` reference it.
- Use the `new-project` skill for both projects; namespace `Curl.Zstandard`. They sort directly after `Curl.Tls.UnitTests` in `Curl.slnx`.
- `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` keeps a map of each hand-built library to what it may reference and data rows per library; it needs the new library (references nothing) and `Curl.Tls.UnitLibrary`'s entry widened with `Curl.Zstandard.UnitLibrary`.

## Acceptance criteria

- [ ] `Curl.Zstandard.UnitLibrary/Curl.Zstandard.UnitLibrary.csproj` and `Curl.Zstandard.UnitTests/Curl.Zstandard.UnitTests.csproj` exist, are listed in `Curl.slnx` in name order, and the library has no `ProjectReference` or `PackageReference`.
- [ ] `Curl.Zstandard.UnitLibrary/CLAUDE.md` states what it holds (ADR-0185) and that it may reference nothing but the BCL.
- [ ] `ProtocolIsolationTests` accepts `Curl.Zstandard.UnitLibrary` as a protocol library's reference, fails any reference from it to another project, and accepts `Curl.Tls.UnitLibrary` -> `Curl.Zstandard.UnitLibrary`, each pinned by a data row.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 3 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-857-lane-3-20260928-212155; start with git cherry-pick --no-commit factory/BL-857-lane-3-20260928-212155 and fix it.
- 2026-09-29: Backlog -> Doing.

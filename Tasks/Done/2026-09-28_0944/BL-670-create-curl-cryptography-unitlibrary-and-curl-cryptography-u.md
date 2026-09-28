---
id: BL-670
title: Create Curl.Cryptography.UnitLibrary and Curl.Cryptography.UnitTests
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-669]
touches: [Curl.slnx, Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-670 — Create Curl.Cryptography.UnitLibrary and Curl.Cryptography.UnitTests

## Goal

`Curl.Cryptography.UnitLibrary` and `Curl.Cryptography.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates like every other library.

## Context

- Follow `.claude/skills/new-project/SKILL.md` step by step (classlib and mstest, `net10.0`, no solution folder, test project references the library, no `Version` attributes, no `IsAotCompatible` or `PublishAot` in either csproj).
- Design: BL-669's ADR (namespace, what the library may reference: the BCL only).
- Write `Curl.Cryptography.UnitLibrary/CLAUDE.md`: purpose (hand-built cryptographic primitives the BCL lacks), BCL only, constant-time and zeroing rules from the ADR, every primitive pinned by published test vectors.
- Leave no placeholder `Class1.cs` or `Test1.cs`; if an empty library cannot reach the coverage gate, add the smallest real type the ADR names (for example the shared constant-time helpers) with its tests.

## Acceptance criteria

- [x] Both projects exist, `Curl.slnx` lists them one after the other in alphabetical order with no solution folder, and `Curl.Cryptography.UnitTests` references `Curl.Cryptography.UnitLibrary`.
- [x] `Curl.Cryptography.UnitLibrary/CLAUDE.md` exists as described.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` runs and reports no failing member.

## Notes

- Scaffolded with `dotnet new classlib` / `mstest`; stripped everything `Directory.Build.props` already sets (target framework, nullable, implicit usings, language version), the MSTest `Version` attribute, and the template's own `MSTestSettings.cs` (the root one is linked into every test project). `Class1.cs` and `Test1.cs` deleted.
- An empty library produces no coverage row, so the smallest real type went in: `ConstantTime` (internal; `MaskFromBit`, `Select`, `ConditionalSwap`, `IsAllZero`), the branch-free helpers ADR-0118's constant-time rules call for and X25519 (BL-671) needs first. `InternalsVisibleTo` lets `Curl.Cryptography.UnitTests` test it. 13 tests.
- `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary`: 100% line, 100% branch, 4 members, 0 failing, worst CRAP 4.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Cryptography.UnitLibrary and .UnitTests exist in Curl.slnx, build clean, and meet the quality gates with the ConstantTime helpers

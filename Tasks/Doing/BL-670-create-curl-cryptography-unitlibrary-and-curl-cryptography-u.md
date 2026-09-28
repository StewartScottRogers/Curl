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
completed:
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

- [ ] Both projects exist, `Curl.slnx` lists them one after the other in alphabetical order with no solution folder, and `Curl.Cryptography.UnitTests` references `Curl.Cryptography.UnitLibrary`.
- [ ] `Curl.Cryptography.UnitLibrary/CLAUDE.md` exists as described.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` runs and reports no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.

---
id: BL-720
title: Create Curl.Http3.UnitLibrary and Curl.Http3.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-718]
touches: [Curl.slnx, Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-720 — Create Curl.Http3.UnitLibrary and Curl.Http3.UnitTests

## Goal

`Curl.Http3.UnitLibrary` and `Curl.Http3.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates.

## Context

- Design: BL-718's ADR; reference rules: BL-667's ADR. Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings, no solution folder). The reference to `Curl.Http2.UnitLibrary` (for the shared Huffman codec) is added by BL-729.
- Write `Curl.Http3.UnitLibrary/CLAUDE.md`: purpose (QPACK per RFC 9204 and HTTP/3 framing per RFC 9114 over QUIC streams given as byte streams), BCL plus `Curl.Http2.UnitLibrary` only, never a `Socket`, no reference to `Curl.Quic.UnitLibrary` (streams arrive through Abstractions contracts).
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [ ] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [ ] `Curl.Http3.UnitLibrary/CLAUDE.md` exists as described.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.

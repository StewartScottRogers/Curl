---
id: BL-719
title: Create Curl.Quic.UnitLibrary and Curl.Quic.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-718]
touches: [Curl.slnx, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-719 — Create Curl.Quic.UnitLibrary and Curl.Quic.UnitTests

## Goal

`Curl.Quic.UnitLibrary` and `Curl.Quic.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates.

## Context

- Design: BL-718's ADR; reference rules: BL-667's ADR. Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings, no solution folder). Add references to `Curl.Tls.UnitLibrary` and `Curl.Cryptography.UnitLibrary` when the first task needs them (BL-723), not here.
- Write `Curl.Quic.UnitLibrary/CLAUDE.md`: purpose (QUIC v1 client transport per RFC 9000, 9001, 9002), BCL plus the hand-built libraries the ADR names, never a `Socket` (datagrams in and out through a seam), time through `TimeProvider`, randomness injected, RFC 9001 Appendix A as the reference vectors.
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [ ] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [ ] `Curl.Quic.UnitLibrary/CLAUDE.md` exists as described.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.

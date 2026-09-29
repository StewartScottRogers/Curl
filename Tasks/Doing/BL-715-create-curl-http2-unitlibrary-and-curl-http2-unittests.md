---
id: BL-715
title: Create Curl.Http2.UnitLibrary and Curl.Http2.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-655]
touches: [Curl.slnx, Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-715 — Create Curl.Http2.UnitLibrary and Curl.Http2.UnitTests

## Goal

`Curl.Http2.UnitLibrary` and `Curl.Http2.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates, ready for HPACK (BL-656) and the frame layer (BL-657).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): the BCL's HPACK and HTTP/2 framing are internal to `System.Net.Http`, so they are hand-built and isolated in their own library. `Curl.Protocol.Http.UnitLibrary` references it (allowed by BL-667's ADR and BL-668's test) and `Curl.Http3.UnitLibrary` reuses its Huffman codec for QPACK. Design: BL-655's ADR.
- Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings, no solution folder).
- Write `Curl.Http2.UnitLibrary/CLAUDE.md`: purpose (HPACK per RFC 7541 and HTTP/2 framing per RFC 9113 over a byte stream), BCL only, never a `Socket` or `SslStream`, frames over an injected stream so tests replay bytes.
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [ ] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [ ] `Curl.Http2.UnitLibrary/CLAUDE.md` exists as described.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.

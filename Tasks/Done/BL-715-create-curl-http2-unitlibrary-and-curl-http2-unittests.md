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
completed: 2026-09-28
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

- [x] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [x] `Curl.Http2.UnitLibrary/CLAUDE.md` exists as described.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Csproj files mirror `Curl.Tls.UnitLibrary` and `.UnitTests` (BL-696): InternalsVisibleTo for the tests, MSTest from central package management, global `Using` for MSTest; written by hand rather than from `dotnet new`, so no template `Class1.cs`, `Test1.cs` or `MSTestSettings.cs` exists.
- `Curl.slnx` lists the pair after `Curl.Cryptography.UnitTests`, before `Curl.Networking.UnitLibrary`, the alphabetical place.
- The test project holds no test yet, so `dotnet test` reports "No test matches" for it and still exits 0, as for `Curl.Tls.UnitTests` when it was scaffolded.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Http2.UnitLibrary and Curl.Http2.UnitTests exist, are in Curl.slnx and build clean

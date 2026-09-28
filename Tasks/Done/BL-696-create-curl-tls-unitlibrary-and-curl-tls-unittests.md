---
id: BL-696
title: Create Curl.Tls.UnitLibrary and Curl.Tls.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-695]
touches: [Curl.slnx, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-696 — Create Curl.Tls.UnitLibrary and Curl.Tls.UnitTests

## Goal

`Curl.Tls.UnitLibrary` and `Curl.Tls.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates.

## Context

- Design: BL-695's ADR; reference rules: BL-667's ADR. Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings, no solution folder). Add the `Curl.Cryptography.UnitLibrary` reference when the first task needs it (BL-699), not here.
- Write `Curl.Tls.UnitLibrary/CLAUDE.md`: purpose (hand-built TLS client for what `SslStream` cannot do, and for QUIC), BCL plus `Curl.Cryptography.UnitLibrary` only, never a `Socket` or `SslStream`, byte streams and handshake messages in and out, randomness and time injected, RFC 8448 traces as the reference tests.
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [x] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [x] `Curl.Tls.UnitLibrary/CLAUDE.md` exists as described.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Csproj files mirror `Curl.Cryptography.UnitLibrary` and `.UnitTests` (InternalsVisibleTo for the tests, MSTest from central package management, global `Using` for MSTest); the template's `MSTestSettings.cs` was dropped, as no other test project has one.
- `Curl.slnx` lists the pair after `Curl.Protocol.Ws.UnitTests`, the alphabetical place.
- The test project holds no test yet, so `dotnet test` reports "No test matches" for it and still exits 0, as for the other empty scaffolds (Ldap, Smb, Rtsp).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Tls.UnitLibrary and Curl.Tls.UnitTests exist, are in Curl.slnx and build clean

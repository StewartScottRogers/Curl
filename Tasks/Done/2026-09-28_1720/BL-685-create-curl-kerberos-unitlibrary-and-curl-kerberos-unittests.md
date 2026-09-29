---
id: BL-685
title: Create Curl.Kerberos.UnitLibrary and Curl.Kerberos.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-525]
touches: [Curl.slnx, Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-685 — Create Curl.Kerberos.UnitLibrary and Curl.Kerberos.UnitTests

## Goal

`Curl.Kerberos.UnitLibrary` and `Curl.Kerberos.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): Kerberos and GSS-API work on every platform, hand-built where the BCL lacks them, isolated in their own library. The library holds a Kerberos V5 client (RFC 4120) and the GSS-API Kerberos mechanism (RFC 4121), shared by HTTP and proxy Negotiate (`Curl.Authentication.UnitLibrary`), SASL `GSSAPI` (BL-538), SOCKS5 GSS-API (BL-615) and FTP `--krb` (BL-693). Design: BL-525's ADR; reference rules: BL-667's ADR.
- Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings, no solution folder). References `Curl.Cryptography.UnitLibrary` only once BL-686 needs it.
- Write `Curl.Kerberos.UnitLibrary/CLAUDE.md`: purpose, BCL plus `Curl.Cryptography.UnitLibrary` only, never a `Socket` (the KDC is reached through an injected transport), files read through an injected seam, time through `TimeProvider`, randomness injected.
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [x] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [x] `Curl.Kerberos.UnitLibrary/CLAUDE.md` exists as described.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Scaffolded with `dotnet new classlib` / `dotnet new mstest`, then both csproj files were reduced to the shape of `Curl.Cryptography`'s (no `Version`, nothing `Directory.Build.props` already sets, `InternalsVisibleTo` the tests). `Class1.cs`, `Test1.cs` and `MSTestSettings.cs` removed.
- The library holds no type yet: an empty `Curl.Kerberos.UnitTests` still exits 0 under `dotnet test`, so no stand-in type was invented; BL-686 adds the first code and the `Curl.Cryptography.UnitLibrary` reference.
- `Measure-CodeQuality.ps1` picks libraries up by the `*.UnitLibrary` name, so the gates measure this one as soon as it has code.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Kerberos.UnitLibrary and Curl.Kerberos.UnitTests exist, are in Curl.slnx in sorted place and build clean

---
id: BL-682
title: Create Curl.Ntlm.UnitLibrary and Curl.Ntlm.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-525]
touches: [Curl.slnx, Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-682 — Create Curl.Ntlm.UnitLibrary and Curl.Ntlm.UnitTests

## Goal

`Curl.Ntlm.UnitLibrary` and `Curl.Ntlm.UnitTests` exist at the repository root, are in `Curl.slnx` in their sorted place, build clean and are measured by the quality gates.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): NTLM works on every platform, hand-built where the BCL lacks it, isolated in its own library. It is shared by HTTP and proxy auth (`Curl.Authentication.UnitLibrary`), SASL `NTLM`, SPNEGO (BL-692) and SMB (`Curl.Protocol.Smb.UnitLibrary`), so it cannot live in any one of them. Design: BL-525's ADR; reference rules: BL-667's ADR.
- Follow `.claude/skills/new-project/SKILL.md` step by step (no `Version` attributes, no AOT settings in either csproj, no solution folder). Reference `Curl.Cryptography.UnitLibrary` only when BL-684 needs MD4, not here.
- Write `Curl.Ntlm.UnitLibrary/CLAUDE.md`: purpose (NTLM message encoding and response computation per MS-NLMP), BCL plus `Curl.Cryptography.UnitLibrary` only, no network, random and time injected.
- Leave no placeholder `Class1.cs` or `Test1.cs`.

## Acceptance criteria

- [x] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [x] `Curl.Ntlm.UnitLibrary/CLAUDE.md` exists as described.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Scaffolded with the `new-project` steps, then both csproj files trimmed to match `Curl.Kerberos.*` (BL-685): the library carries only `InternalsVisibleTo` for its tests; the test project only `MSTest`, the global `Using` and the library reference. Template `Class1.cs`, `Test1.cs` and `MSTestSettings.cs` removed (no other test project keeps `MSTestSettings.cs`).
- The test project holds no tests yet, like `Curl.Kerberos.UnitTests`; the fast test run reports "No test matches" for it and passes. BL-683 adds the first ones.
- `Measure-CodeQuality.ps1` picks the library up by its `*.UnitLibrary` name; nothing to register.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Ntlm.UnitLibrary and Curl.Ntlm.UnitTests exist, sit in Curl.slnx in sorted place, build clean and are measured by the quality gates

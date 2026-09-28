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
completed:
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

- [ ] Both projects exist, `Curl.slnx` lists them together in alphabetical order with no solution folder, and the test project references the library.
- [ ] `Curl.Ntlm.UnitLibrary/CLAUDE.md` exists as described.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.

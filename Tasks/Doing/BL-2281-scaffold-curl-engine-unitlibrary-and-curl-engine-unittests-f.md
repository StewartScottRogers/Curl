---
id: BL-2281
title: Scaffold Curl.Engine.UnitLibrary and Curl.Engine.UnitTests for the transfer engine moving out of Curl.Console
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-15
completed:
---
# BL-2281 — Scaffold Curl.Engine.UnitLibrary and Curl.Engine.UnitTests for the transfer engine moving out of Curl.Console

## Goal

`Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` exist in the solution, empty of transfer code, ready for the engine moves (BL-2285 onwards).

## Context

ADR-0506 and `Documentation/Planning/Curl-Api-And-libcurl.md` (phase 1): the transfer engine moves out of `Curl.Console` into its own library, which later implements the seam in `Curl.Api.UnitLibrary`. Use the `new-project` skill's conventions. `Curl.Engine.UnitLibrary` is an ordinary `*.UnitLibrary`, so `CLAUDE.md`'s naming rules and `Measure-CodeQuality.ps1` already cover it; only the project count in root `CLAUDE.md`'s layout block changes, and that belongs to BL-2292, which edits `CLAUDE.md` - leave it.

- References: `Curl.Core`, `Curl.Networking`, `Curl.Tls`, `Curl.Authentication`, `Curl.Cookies`, `Curl.Output`, `Curl.HttpMessageSignatures`, `Curl.Protocol.Abstractions` and every `Curl.Protocol.*` library `Curl.Console` references today (copy the list from `Curl.Console/Curl.Console.csproj`), plus `Curl.Cli.UnitLibrary` - a temporary reference the engine drops in BL-2310.
- `InternalsVisibleTo`: `curl` (the assembly name of `Curl.Console`, not its project name), `Curl.Console.UnitTests`, `Curl.Console.IntegrationTests`, `Curl.Conformance.UnitTests`, `Curl.Engine.UnitTests`.
- `Curl.slnx`: the two projects sit between `Curl.Cryptography.UnitTests` and `Curl.Http2.UnitLibrary`, in the flat run.
- `Curl.Console` does not reference the engine yet; BL-2285 adds that with the first move.

## Acceptance criteria

- [ ] `Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj` and `Curl.Engine.UnitTests/Curl.Engine.UnitTests.csproj` exist, are listed in `Curl.slnx` between `Curl.Cryptography.UnitTests` and `Curl.Http2.UnitLibrary`, and the library has the references and `InternalsVisibleTo` entries listed in Context.
- [ ] The library's root namespace is `Curl.Engine` (by `Directory.Build.props`' rule) and it has a `CLAUDE.md` stating its purpose (the managed transfer engine, ADR-0506) and that it must never reference `Curl.Console`, `Libcurl` or `Curl.Api`.
- [ ] `dotnet build` is clean (warnings are errors), including `VerifyAotCompatibility`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.

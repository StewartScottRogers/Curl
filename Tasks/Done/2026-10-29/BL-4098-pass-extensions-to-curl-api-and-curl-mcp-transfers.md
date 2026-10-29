---
id: BL-4098
title: Pass extensions to Curl.Api and Curl.Mcp transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4083, BL-4167]
touches: [Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitTests/ManagedCurlEngineExtensionTests.cs, Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests/CurlTransferToolIntegrationTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4098 — Pass extensions to Curl.Api and Curl.Mcp transfers

## Goal

Transfers run through `Curl.Api` and `Curl.Mcp` (via `ManagedCurlEngine`) reach the same extensions as the `curl` tool, and `CURL_EXTENSIONS=0` turns them off there too.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". ADR-0999 wired only `Curl.Console`; `Libcurl` stays without extensions to match real libcurl.

## Acceptance criteria

- [x] A `Curl.Api` unit test runs a transfer to a test extension's scheme and reaches its handler.
- [x] A `Curl.Mcp` unit test does the same through the MCP transfer tool.
- [x] With `CURL_EXTENSIONS=0` both refuse the scheme as an unsupported protocol (unit tests).
- [x] `Libcurl` still references no extension project (the isolation build check passes).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan and work (uncommitted, in the stash): `ManagedCurlEngine` gains a public constructor taking the extension handler factory, passed to `EngineComposition.CreateProtocolHandlers` by the production dispatcher; `CurlClient.Create(options)` and `CurlMcpHost.CreateRegistry(root, cap)` pass `ExtensionProtocolHandlers.Create` unless `CURL_EXTENSIONS=0` (internal overloads take the reader and factory for tests); `Curl.Api.csproj` and `Curl.Mcp.csproj` reference `Curl.Extension.Composition.UnitLibrary`. Tests: `ManagedCurlEngineExtensionTests`, `CurlClientCreateExtensionTests`, `CurlMcpHostExtensionTests` (all pass). Libcurl unchanged.
- Touches widened to Curl.Engine.UnitLibrary/ManagedCurlEngine.cs and a new Engine test file: the engine had no public way to take extension handlers. No task in Doing on origin/work/dark-factory named them.
- Decision (sensible default): the switch reuses `ExtensionProtocolHandlers.For` rather than a new overload, since BL-4092 holds ExtensionProtocolHandlers.cs.
- Measured 2026-10-29 09:14: build clean, fast tests green; Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary, Curl.Api, Curl.Mcp has 2 failing members, neither changed here: `EngineComposition.CreateProtocolHandlers` complexity 12 (BL-4083 covers it) and `CurlMcpHost.Build()` branch 50% (filed BL-4167). Requeued until both are Done; then reapply the stash, rerun the quality measurement, write the ADR, and finish.

- 2026-10-29 (second claim): the quality run found `CurlMcpHost.Build()` at 50% branch again: the new `Environment.GetEnvironmentVariable` method group in the two-argument `CreateRegistry` shared the compiler's delegate cache with `Build()`, so tests that ran first set it. Fixed by removing that overload: `CreateHandlers` takes the host's injected reader and passes it to `CreateRegistry`. Touches widened to `Curl.Mcp.IntegrationTests/CurlTransferToolIntegrationTests.cs`, whose one call used the removed overload; no task in Doing on origin/work/dark-factory named it.
- Measured: `Measure-CodeQuality.ps1` for Curl.Engine.UnitLibrary and Curl.Api 0 failing members, Curl.Mcp 0 after the fix. Build clean (its isolation check passes; Libcurl references no extension), fast tests green on Windows. The new tests use no path, file or OS text, so they are platform-neutral. Decision recorded in ADR-1071.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 40a3375d 4378bd6c 0bd084c8 c934e2ba e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waiting on BL-4083 (EngineComposition.CreateProtocolHandlers complexity) and BL-4167 (CurlMcpHost.Build branch), the two quality-gate failures in libraries this task changes
- 2026-10-29: Backlog -> Doing. Criteria at claim: 40a3375d 4378bd6c 0bd084c8 c934e2ba e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Api and Curl.Mcp transfers reach Curl's extensions, off under CURL_EXTENSIONS=0

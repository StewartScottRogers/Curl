---
id: BL-2561
title: Pack only Curl.Mcp's tool package in its pack Integration test so CI's mcp-pack job fits its time limit
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.IntegrationTests]
lane: no
requirement: none
created: 2026-10-17
completed: 2026-10-16
---
# BL-2561 — Pack only Curl.Mcp's tool package in its pack Integration test so CI's mcp-pack job fits its time limit

## Goal

`Pack_MakesAnMcpServerPackageHoldingServerJson` packs only the Curl.Mcp tool package, so ci.yml's `mcp-pack` job finishes inside its 30-minute limit on Windows and Ubuntu.

## Context

Every CI run on work/dark-factory since 2026-10-16 14:59 UTC was cancelled: "Pack Curl.Mcp" hit `timeout-minutes: 30` on windows-latest and ubuntu-latest (macos-latest passes in about 2 minutes). With CI never green, shift 20261016-081911's merge to master could not happen.

Measured: when it still passed, the Curl.Mcp Integration tests step took 15 min 41 s on Ubuntu, almost all of it `Pack_MakesAnMcpServerPackageHoldingServerJson` (15 min 38 s locally on Windows). A plain `dotnet pack Curl.Mcp` builds all six self-contained runtime packages, but the test checks only the tool package's `.mcp/server.json` and `McpServer` package type. BL-2486's AOT publish test (CurlMcpNativeAotIntegrationTests, 2026-10-16 16:24 UTC) added more time and pushed the job past 30 minutes on every run.

`Curl.Mcp.csproj` says with `-p:PublishAot=true` a plain `dotnet pack` makes only the tool package (ADR-0599). The job's own "Pack Curl.Mcp" step still checks all seven packages.

## Acceptance criteria

- [x] `Pack_MakesAnMcpServerPackageHoldingServerJson` packs with `-p:PublishAot=true` and still checks `.mcp/server.json` and the `McpServer` package type in `Curl.Mcp.0.1.0.nupkg`.
- [x] The test runs in under 2 minutes locally (measured time in Notes).
- [x] `dotnet build` is clean and the fast tests pass; `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration"` passes.
- [x] A CI run on work/dark-factory has "Pack Curl.Mcp" green on Windows, Linux and macOS.

## Notes

- `-p:PublishAot=true` alone did not shorten the test locally: still 15 min 18 s. The local test assembly is built in Debug, so a `-c Release` pack compiled every referenced project cold in Release. Warm, the same Release pack took 11 s. The test now packs in the configuration its own assembly was built in (`AssemblyConfigurationAttribute`): Debug locally, Release in ci.yml's mcp-pack job, which builds the tests with `-c Release`.
- Measured locally on Windows after the change: the test took 11 s (the `dotnet test` run, including the incremental build, took 33 s). All 12 Curl.Mcp Integration tests passed in 1 min 4 s.
- CI run 38993851342 (3b0226b17) still cancelled Windows and Ubuntu pack at 30 minutes: both jobs built in about 2 minutes, then sat silent in the test run. Real cause: the pack and the AOT publish tests redirect the child's output, and the MSBuild nodes and compiler server the child starts inherit those pipes and hold them open until they go idle, about 15 minutes, so `ReadToEndAsync` waited 15 minutes per test after the child had exited. That also explains the first local run's 15 min 18 s. Both tests now pass `--disable-build-servers`. After `dotnet build-server shutdown`, all 12 Curl.Mcp Integration tests passed in 1 min 56 s with no build server left running.

## Log

- 2026-10-17: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: d67afc00 06d04747 63c8c8b4 90196da2.
- CI run 38996150045 (7c5f3d00c) green on every job: Pack Curl.Mcp took 4 min on Ubuntu, 9 min on Windows and 5 min on macOS.
- 2026-10-16: Doing -> Done. Both SDK-running Curl.Mcp Integration tests pass --disable-build-servers and the pack test makes only the tool package; CI run 38996150045 green on Windows, Linux and macOS.

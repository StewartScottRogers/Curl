---
id: BL-2295
title: Define EasyNetworkOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed:
---
# BL-2295 — Define EasyNetworkOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Network` (namespace `Curl.Api.Options.Network`) defines `EasyNetworkOptions`, an immutable record carrying every option of the plan's `EasyNetworkOptions` row (URL, protocols, ports, interfaces, connect-to and resolve, DNS, IP resolve, TCP, Unix sockets, connect timeouts, connection reuse, connect-only, upkeep, `USE_SSL`), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context (sealed record, enums in the same folder, `TimeSpan` for durations, XML doc naming each `CURLOPT_*` and its default from curl 8.22.0's `docs/libcurl/opts`, no protocol or engine reference). `CURLOPT_RESOLVE` and `CURLOPT_CONNECT_TO` keep their entry syntax as strings; parsing them stays the engine's job. BL-2303 retargets the engine onto this record.

## Acceptance criteria

- [ ] `EasyNetworkOptions` exists in `Curl.Api.UnitLibrary/Options/Network` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Network` pin every default and that a `with` copy changes only the property set.
- [ ] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
